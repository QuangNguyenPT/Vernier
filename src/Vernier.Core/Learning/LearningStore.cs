using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Vernier.Core.Learning;

public sealed record ConceptState(long Id, string Subject, string Name, string Summary, MasteryAssessment Mastery, int OpenMisconceptions, int EvidenceCount, bool IsSample);
public sealed record SessionInfo(long Id, DateTimeOffset StartedAt, string Title, string Mode, int MessageCount);
public sealed record SessionMessage(long Id, long SessionId, string Role, string Text, DateTimeOffset At);
public sealed record SearchHit(long SessionId, string SessionTitle, string Role, string Snippet, DateTimeOffset At);
public sealed record Card(long Id, long? ConceptId, string Concept, string Front, string Back, CardSchedule Schedule, bool IsSample);
public sealed record Misconception(long Id, long ConceptId, string Concept, string Description, DateTimeOffset At, bool Resolved);
public sealed record Formula(long Id, string Concept, string Expression, string Meaning, string Derivation);
public sealed record WorkedExample(long Id, string Concept, string Problem, string Solution);
public sealed record StudyItem(string Kind, string Title, string Reason, double Priority, long? CardId, long? ConceptId);

/// <summary>
/// Local SQLite learning record. Schema is versioned through PRAGMA user_version; each
/// migration runs once inside a transaction. Nothing here ever leaves the machine by itself.
/// </summary>
public sealed class LearningStore : IDisposable
{
    private static readonly string[] Migrations =
    [
        // v1
        """
        CREATE TABLE subjects(id INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE COLLATE NOCASE, is_sample INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE concepts(id INTEGER PRIMARY KEY, subject_id INTEGER NOT NULL REFERENCES subjects(id) ON DELETE CASCADE,
            name TEXT NOT NULL COLLATE NOCASE, summary TEXT NOT NULL DEFAULT '', is_sample INTEGER NOT NULL DEFAULT 0, UNIQUE(subject_id, name));
        CREATE TABLE concept_prereqs(concept_id INTEGER NOT NULL REFERENCES concepts(id) ON DELETE CASCADE,
            prereq_id INTEGER NOT NULL REFERENCES concepts(id) ON DELETE CASCADE, PRIMARY KEY(concept_id, prereq_id));
        CREATE TABLE sessions(id INTEGER PRIMARY KEY, started_at TEXT NOT NULL, title TEXT NOT NULL, mode TEXT NOT NULL, is_sample INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE messages(id INTEGER PRIMARY KEY, session_id INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
            role TEXT NOT NULL, text TEXT NOT NULL, at TEXT NOT NULL);
        CREATE VIRTUAL TABLE messages_fts USING fts5(text, content='messages', content_rowid='id');
        CREATE TRIGGER messages_ai AFTER INSERT ON messages BEGIN INSERT INTO messages_fts(rowid, text) VALUES (new.id, new.text); END;
        CREATE TRIGGER messages_ad AFTER DELETE ON messages BEGIN INSERT INTO messages_fts(messages_fts, rowid, text) VALUES ('delete', old.id, old.text); END;
        CREATE TABLE evidence(id INTEGER PRIMARY KEY, concept_id INTEGER NOT NULL REFERENCES concepts(id) ON DELETE CASCADE,
            kind TEXT NOT NULL, correct INTEGER NOT NULL, at TEXT NOT NULL);
        CREATE TABLE misconceptions(id INTEGER PRIMARY KEY, concept_id INTEGER NOT NULL REFERENCES concepts(id) ON DELETE CASCADE,
            description TEXT NOT NULL, at TEXT NOT NULL, resolved INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE cards(id INTEGER PRIMARY KEY, concept_id INTEGER REFERENCES concepts(id) ON DELETE CASCADE,
            front TEXT NOT NULL, back TEXT NOT NULL, ease REAL NOT NULL, interval_days INTEGER NOT NULL, reps INTEGER NOT NULL,
            due_at TEXT NOT NULL, is_sample INTEGER NOT NULL DEFAULT 0);
        CREATE TABLE reviews(id INTEGER PRIMARY KEY, card_id INTEGER NOT NULL REFERENCES cards(id) ON DELETE CASCADE, at TEXT NOT NULL, grade INTEGER NOT NULL);
        CREATE TABLE formulas(id INTEGER PRIMARY KEY, concept_id INTEGER NOT NULL REFERENCES concepts(id) ON DELETE CASCADE,
            expression TEXT NOT NULL, meaning TEXT NOT NULL DEFAULT '', derivation TEXT NOT NULL DEFAULT '');
        CREATE TABLE examples(id INTEGER PRIMARY KEY, concept_id INTEGER NOT NULL REFERENCES concepts(id) ON DELETE CASCADE,
            problem TEXT NOT NULL, solution TEXT NOT NULL);
        """,
        // v2
        """
        CREATE TABLE open_questions(id INTEGER PRIMARY KEY, concept_id INTEGER REFERENCES concepts(id) ON DELETE CASCADE,
            question TEXT NOT NULL, at TEXT NOT NULL, answered INTEGER NOT NULL DEFAULT 0);
        CREATE INDEX ix_cards_due ON cards(due_at);
        CREATE INDEX ix_evidence_concept ON evidence(concept_id);
        """,
        // v3: episodes of work. Titles and cleaned addresses only; screen text never reaches the disk.
        """
        CREATE TABLE IF NOT EXISTS episodes(id INTEGER PRIMARY KEY, app TEXT NOT NULL, key TEXT NOT NULL, title TEXT NOT NULL, url TEXT,
            started_at TEXT NOT NULL, ended_at TEXT NOT NULL, active_seconds REAL NOT NULL DEFAULT 0,
            media_title TEXT, media_position REAL, media_duration REAL, rewinds TEXT, questions INTEGER NOT NULL DEFAULT 0,
            last_question TEXT, summary TEXT);
        CREATE INDEX IF NOT EXISTS ix_episodes_ended ON episodes(ended_at);
        """,
    ];

    public static int LatestVersion => Migrations.Length;

    private readonly string _path;
    private SqliteConnection _db = null!;
    private readonly object _gate = new();

    public string Path => _path;
    public string? RecoveryNotice { get; private set; }

    public LearningStore(string path)
    {
        _path = path;
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        try
        {
            Open();
        }
        catch (SqliteException)
        {
            Recover("could not be opened");
        }
    }

    private void Open()
    {
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString());
        _db.Open();
        Exec("PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;");
        if (Scalar<string>("PRAGMA integrity_check") != "ok") throw new SqliteException("integrity_check failed", 11);
        Migrate();
    }

    /// <summary>Sets the damaged file aside, restores the newest backup if there is one, else starts clean.</summary>
    private void Recover(string why)
    {
        try { _db?.Dispose(); } catch (SqliteException) { }
        SqliteConnection.ClearAllPools();
        var aside = _path + ".damaged-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        if (File.Exists(_path)) File.Move(_path, aside, overwrite: true);
        foreach (var side in new[] { _path + "-wal", _path + "-shm" })
            if (File.Exists(side)) File.Delete(side);

        var backup = LatestBackup();
        if (backup is not null) File.Copy(backup, _path);
        RecoveryNotice = backup is not null
            ? $"The learning database {why}. It was restored from the backup {System.IO.Path.GetFileName(backup)}; the damaged file was kept as {System.IO.Path.GetFileName(aside)}."
            : $"The learning database {why} and no backup existed. A new one was created; the damaged file was kept as {System.IO.Path.GetFileName(aside)}.";
        Open();
    }

    private void Migrate()
    {
        var version = (int)Scalar<long>("PRAGMA user_version");
        if (version > Migrations.Length)
            throw new InvalidOperationException($"The learning database is version {version}, newer than this build understands ({Migrations.Length}). Update Vernier; the file was not modified.");
        if (version > 0 && version < Migrations.Length) BackupBeforeMigration(version);
        for (var v = version; v < Migrations.Length; v++)
        {
            using var tx = _db.BeginTransaction();
            Exec(Migrations[v], tx);
            Exec($"PRAGMA user_version = {v + 1}", tx);
            tx.Commit();
        }
    }

    public int SchemaVersion => (int)Scalar<long>("PRAGMA user_version");

    // ---------- subjects / concepts ----------

    public long EnsureSubject(string name, bool sample = false)
    {
        name = Clean(name, "General");
        lock (_gate)
        {
            Exec("INSERT OR IGNORE INTO subjects(name, is_sample) VALUES ($n, $s)", null, ("$n", name), ("$s", sample));
            return Scalar<long>("SELECT id FROM subjects WHERE name = $n", ("$n", name));
        }
    }

    public long EnsureConcept(string subject, string concept, string? summary = null, bool sample = false)
    {
        concept = Clean(concept, "Untitled concept");
        var subjectId = EnsureSubject(subject, sample);
        lock (_gate)
        {
            Exec("INSERT OR IGNORE INTO concepts(subject_id, name, summary, is_sample) VALUES ($s, $n, $m, $x)", null,
                ("$s", subjectId), ("$n", concept), ("$m", summary ?? ""), ("$x", sample));
            if (!string.IsNullOrWhiteSpace(summary))
                Exec("UPDATE concepts SET summary = $m WHERE subject_id = $s AND name = $n AND summary = ''", null,
                    ("$s", subjectId), ("$n", concept), ("$m", summary));
            return Scalar<long>("SELECT id FROM concepts WHERE subject_id = $s AND name = $n", ("$s", subjectId), ("$n", concept));
        }
    }

    public void AddPrerequisite(long conceptId, string subject, string prerequisiteName)
    {
        var prereq = EnsureConcept(subject, prerequisiteName);
        if (prereq == conceptId) return;
        lock (_gate)
            Exec("INSERT OR IGNORE INTO concept_prereqs(concept_id, prereq_id) VALUES ($c, $p)", null, ("$c", conceptId), ("$p", prereq));
    }

    public IReadOnlyList<string> Prerequisites(long conceptId) =>
        Query("SELECT c.name FROM concept_prereqs p JOIN concepts c ON c.id = p.prereq_id WHERE p.concept_id = $c ORDER BY c.name",
            r => r.GetString(0), ("$c", conceptId));

    public IReadOnlyList<ConceptState> ConceptStates()
    {
        var concepts = Query(
            """
            SELECT c.id, s.name, c.name, c.summary, c.is_sample,
                   (SELECT COUNT(*) FROM misconceptions m WHERE m.concept_id = c.id AND m.resolved = 0)
            FROM concepts c JOIN subjects s ON s.id = c.subject_id ORDER BY s.name, c.name
            """,
            r => (Id: r.GetInt64(0), Subject: r.GetString(1), Name: r.GetString(2), Summary: r.GetString(3), Sample: r.GetBoolean(4), Open: r.GetInt32(5)));
        var result = new List<ConceptState>(concepts.Count);
        foreach (var c in concepts)
        {
            var ev = EvidenceFor(c.Id);
            result.Add(new ConceptState(c.Id, c.Subject, c.Name, c.Summary, MasteryModel.Assess(ev, c.Open), c.Open, ev.Count, c.Sample));
        }
        return result;
    }

    // ---------- evidence / misconceptions / questions ----------

    public void AddEvidence(long conceptId, string kind, bool correct, DateTimeOffset? at = null)
    {
        if (!EvidenceKinds.IsKnown(kind)) kind = EvidenceKinds.Diagnostic;
        lock (_gate)
            Exec("INSERT INTO evidence(concept_id, kind, correct, at) VALUES ($c, $k, $r, $a)", null,
                ("$c", conceptId), ("$k", kind), ("$r", correct), ("$a", Iso(at ?? DateTimeOffset.UtcNow)));
    }

    public IReadOnlyList<Evidence> EvidenceFor(long conceptId) =>
        Query("SELECT kind, correct, at FROM evidence WHERE concept_id = $c ORDER BY at, id",
            r => new Evidence(r.GetString(0), r.GetBoolean(1), Date(r.GetString(2))), ("$c", conceptId));

    public void AddMisconception(long conceptId, string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return;
        lock (_gate)
        {
            var exists = Scalar<long>("SELECT COUNT(*) FROM misconceptions WHERE concept_id = $c AND description = $d AND resolved = 0",
                ("$c", conceptId), ("$d", description.Trim()));
            if (exists == 0)
                Exec("INSERT INTO misconceptions(concept_id, description, at) VALUES ($c, $d, $a)", null,
                    ("$c", conceptId), ("$d", description.Trim()), ("$a", Iso(DateTimeOffset.UtcNow)));
        }
    }

    public IReadOnlyList<Misconception> Misconceptions(bool includeResolved = true) =>
        Query(
            $"""
            SELECT m.id, m.concept_id, c.name, m.description, m.at, m.resolved FROM misconceptions m
            JOIN concepts c ON c.id = m.concept_id {(includeResolved ? "" : "WHERE m.resolved = 0")} ORDER BY m.resolved, m.at DESC
            """,
            r => new Misconception(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), Date(r.GetString(4)), r.GetBoolean(5)));

    public void ResolveMisconception(long id, bool resolved = true)
    {
        lock (_gate) Exec("UPDATE misconceptions SET resolved = $r WHERE id = $i", null, ("$r", resolved), ("$i", id));
    }

    public void AddOpenQuestion(long? conceptId, string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return;
        lock (_gate)
            Exec("INSERT INTO open_questions(concept_id, question, at) VALUES ($c, $q, $a)", null,
                ("$c", (object?)conceptId ?? DBNull.Value), ("$q", question.Trim()), ("$a", Iso(DateTimeOffset.UtcNow)));
    }

    public IReadOnlyList<string> OpenQuestions() =>
        Query("SELECT question FROM open_questions WHERE answered = 0 ORDER BY at DESC LIMIT 50", r => r.GetString(0));

    // ---------- sessions ----------

    public long StartSession(string title, string mode, bool sample = false)
    {
        lock (_gate)
        {
            Exec("INSERT INTO sessions(started_at, title, mode, is_sample) VALUES ($a, $t, $m, $s)", null,
                ("$a", Iso(DateTimeOffset.UtcNow)), ("$t", Truncate(Clean(title, "Session"), 120)), ("$m", mode), ("$s", sample));
            return Scalar<long>("SELECT last_insert_rowid()");
        }
    }

    public void AddMessage(long sessionId, string role, string text)
    {
        lock (_gate)
            Exec("INSERT INTO messages(session_id, role, text, at) VALUES ($s, $r, $t, $a)", null,
                ("$s", sessionId), ("$r", role), ("$t", text), ("$a", Iso(DateTimeOffset.UtcNow)));
    }

    public IReadOnlyList<SessionInfo> Sessions() =>
        Query(
            """
            SELECT s.id, s.started_at, s.title, s.mode, (SELECT COUNT(*) FROM messages m WHERE m.session_id = s.id)
            FROM sessions s ORDER BY s.started_at DESC, s.id DESC LIMIT 500
            """,
            r => new SessionInfo(r.GetInt64(0), Date(r.GetString(1)), r.GetString(2), r.GetString(3), r.GetInt32(4)));

    public IReadOnlyList<SessionMessage> Messages(long sessionId) =>
        Query("SELECT id, session_id, role, text, at FROM messages WHERE session_id = $s ORDER BY id",
            r => new SessionMessage(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), Date(r.GetString(4))), ("$s", sessionId));

    public IReadOnlyList<SearchHit> Search(string query)
    {
        // Quote each term so user text is never interpreted as FTS5 query syntax.
        var terms = query.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => "\"" + t.Replace("\"", "\"\"") + "\"*").ToArray();
        if (terms.Length == 0) return [];
        return Query(
            """
            SELECT m.session_id, s.title, m.role, snippet(messages_fts, 0, '[', ']', ' … ', 14), m.at
            FROM messages_fts JOIN messages m ON m.id = messages_fts.rowid JOIN sessions s ON s.id = m.session_id
            WHERE messages_fts MATCH $q ORDER BY rank LIMIT 50
            """,
            r => new SearchHit(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), Date(r.GetString(4))),
            ("$q", string.Join(" ", terms)));
    }

    public void DeleteSession(long id)
    {
        lock (_gate) Exec("DELETE FROM sessions WHERE id = $i", null, ("$i", id));
    }

    public int PurgeSessionsOlderThan(int days, DateTimeOffset now)
    {
        if (days <= 0) return 0;
        lock (_gate)
            return Exec("DELETE FROM sessions WHERE started_at < $a", null, ("$a", Iso(now.AddDays(-days))));
    }

    /// <summary>Erases every learning record. The file stays so the schema does not need rebuilding.</summary>
    public void DeleteEverything()
    {
        lock (_gate)
        {
            using var tx = _db.BeginTransaction();
            Exec("DELETE FROM sessions; DELETE FROM subjects; DELETE FROM cards; DELETE FROM open_questions; DELETE FROM episodes;", tx);
            tx.Commit();
            Exec("INSERT INTO messages_fts(messages_fts) VALUES ('rebuild'); VACUUM;");
        }
    }

    // ---------- cards ----------

    public long AddCard(long? conceptId, string front, string back, bool sample = false, DateTimeOffset? due = null)
    {
        lock (_gate)
        {
            var existing = Scalar<long?>("SELECT id FROM cards WHERE front = $f LIMIT 1", ("$f", front.Trim()));
            if (existing is { } id) return id;
            var s = Sm2.New(due ?? DateTimeOffset.UtcNow);
            Exec("INSERT INTO cards(concept_id, front, back, ease, interval_days, reps, due_at, is_sample) VALUES ($c, $f, $b, $e, $i, $r, $d, $s)", null,
                ("$c", (object?)conceptId ?? DBNull.Value), ("$f", front.Trim()), ("$b", back.Trim()), ("$e", s.Ease),
                ("$i", s.IntervalDays), ("$r", s.Repetitions), ("$d", Iso(s.Due)), ("$s", sample));
            return Scalar<long>("SELECT last_insert_rowid()");
        }
    }

    public IReadOnlyList<Card> Cards(DateTimeOffset? dueBy = null) =>
        Query(
            $"""
            SELECT k.id, k.concept_id, COALESCE(c.name, ''), k.front, k.back, k.ease, k.interval_days, k.reps, k.due_at, k.is_sample
            FROM cards k LEFT JOIN concepts c ON c.id = k.concept_id {(dueBy is null ? "" : "WHERE k.due_at <= $d")} ORDER BY k.due_at, k.id
            """,
            r => new Card(r.GetInt64(0), r.IsDBNull(1) ? null : r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4),
                new CardSchedule(r.GetDouble(5), r.GetInt32(6), r.GetInt32(7), Date(r.GetString(8))), r.GetBoolean(9)),
            ("$d", Iso(dueBy ?? DateTimeOffset.MaxValue)));

    /// <summary>Applies an SM-2 review. A recall grade also counts as mastery evidence for the card's concept.</summary>
    public CardSchedule ReviewCard(long cardId, int grade, DateTimeOffset now, IReadOnlyList<int>? firstIntervals = null)
    {
        var card = Cards().FirstOrDefault(c => c.Id == cardId) ?? throw new InvalidOperationException("Card not found.");
        var next = Sm2.Next(card.Schedule, grade, now, firstIntervals);
        lock (_gate)
        {
            using var tx = _db.BeginTransaction();
            Exec("UPDATE cards SET ease = $e, interval_days = $i, reps = $r, due_at = $d WHERE id = $c", tx,
                ("$e", next.Ease), ("$i", next.IntervalDays), ("$r", next.Repetitions), ("$d", Iso(next.Due)), ("$c", cardId));
            Exec("INSERT INTO reviews(card_id, at, grade) VALUES ($c, $a, $g)", tx, ("$c", cardId), ("$a", Iso(now)), ("$g", grade));
            tx.Commit();
        }
        if (card.ConceptId is { } conceptId) AddEvidence(conceptId, EvidenceKinds.Recall, grade >= 3, now);
        return next;
    }

    public void DeleteCard(long id)
    {
        lock (_gate) Exec("DELETE FROM cards WHERE id = $i", null, ("$i", id));
    }

    // ---------- formulas / examples ----------

    public void AddFormula(long conceptId, string expression, string? meaning, string? derivation)
    {
        if (string.IsNullOrWhiteSpace(expression)) return;
        lock (_gate)
        {
            if (Scalar<long>("SELECT COUNT(*) FROM formulas WHERE concept_id = $c AND expression = $e", ("$c", conceptId), ("$e", expression.Trim())) > 0) return;
            Exec("INSERT INTO formulas(concept_id, expression, meaning, derivation) VALUES ($c, $e, $m, $d)", null,
                ("$c", conceptId), ("$e", expression.Trim()), ("$m", meaning ?? ""), ("$d", derivation ?? ""));
        }
    }

    public IReadOnlyList<Formula> Formulas() =>
        Query("SELECT f.id, c.name, f.expression, f.meaning, f.derivation FROM formulas f JOIN concepts c ON c.id = f.concept_id ORDER BY c.name, f.id",
            r => new Formula(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));

    public void AddExample(long conceptId, string problem, string solution)
    {
        lock (_gate)
            Exec("INSERT INTO examples(concept_id, problem, solution) VALUES ($c, $p, $s)", null, ("$c", conceptId), ("$p", problem), ("$s", solution));
    }

    public IReadOnlyList<WorkedExample> Examples() =>
        Query("SELECT e.id, c.name, e.problem, e.solution FROM examples e JOIN concepts c ON c.id = e.concept_id ORDER BY c.name, e.id",
            r => new WorkedExample(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3)));

    // ---------- study queue ----------

    /// <summary>What to study next: unresolved misconceptions, then overdue cards, then weak concepts.</summary>
    public IReadOnlyList<StudyItem> StudyQueue(DateTimeOffset now, int max = 30)
    {
        var items = new List<StudyItem>();
        foreach (var m in Misconceptions(includeResolved: false))
            items.Add(new StudyItem("misconception", m.Concept, m.Description, 100, null, m.ConceptId));
        foreach (var c in Cards(now))
        {
            var overdueDays = Math.Max(0, (now - c.Schedule.Due).TotalDays);
            items.Add(new StudyItem("card", c.Front, overdueDays >= 1 ? $"Due {overdueDays:0} day(s) ago" : "Due now", 60 + Math.Min(30, overdueDays), c.Id, c.ConceptId));
        }
        foreach (var s in ConceptStates().Where(s => s.Mastery.Level is MasteryLevel.Introduced or MasteryLevel.Practicing && s.OpenMisconceptions == 0))
            items.Add(new StudyItem("concept", s.Name, s.Mastery.Reason, 40 - s.Mastery.Score * 20, null, s.Id));
        return items.OrderByDescending(i => i.Priority).Take(max).ToList();
    }

    // ---------- backup / export / import ----------

    public string BackupDirectory => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path))!, "backups");

    public string Backup(int keep = 7)
    {
        Directory.CreateDirectory(BackupDirectory);
        var target = System.IO.Path.Combine(BackupDirectory, $"learning-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");
        lock (_gate)
        {
            using var dest = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString());
            dest.Open();
            _db.BackupDatabase(dest);
        }
        foreach (var old in Directory.GetFiles(BackupDirectory, "learning-*.db").OrderByDescending(f => f).Skip(keep))
            File.Delete(old);
        return target;
    }

    private string? LatestBackup() =>
        Directory.Exists(BackupDirectory) ? Directory.GetFiles(BackupDirectory, "learning-*.db").OrderByDescending(f => f).FirstOrDefault() : null;

    public sealed record ExportConcept(string Subject, string Name, string Summary, List<string> Prerequisites, List<Evidence> Evidence, List<string> Misconceptions);
    public sealed record ExportCard(string? Concept, string Front, string Back, double Ease, int IntervalDays, int Repetitions, DateTimeOffset Due);
    public sealed record ExportDocument(string Format, int Version, DateTimeOffset ExportedAt, List<ExportConcept> Concepts, List<ExportCard> Cards, List<Formula> Formulas, List<WorkedExample> Examples);

    private static readonly JsonSerializerOptions ExportJson = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    /// <summary>Documented JSON export (format "vernier-learning", see docs/DATA_FORMATS.md). Sample data is excluded.</summary>
    public string Export()
    {
        var concepts = ConceptStates().Where(c => !c.IsSample).Select(c => new ExportConcept(
            c.Subject, c.Name, c.Summary, [.. Prerequisites(c.Id)], [.. EvidenceFor(c.Id)],
            [.. Misconceptions(false).Where(m => m.ConceptId == c.Id).Select(m => m.Description)])).ToList();
        var cards = Cards().Where(c => !c.IsSample).Select(c => new ExportCard(
            c.Concept.Length == 0 ? null : c.Concept, c.Front, c.Back, c.Schedule.Ease, c.Schedule.IntervalDays, c.Schedule.Repetitions, c.Schedule.Due)).ToList();
        return JsonSerializer.Serialize(new ExportDocument("vernier-learning", 1, DateTimeOffset.UtcNow, concepts, cards, [.. Formulas()], [.. Examples()]), ExportJson);
    }

    /// <summary>Merges an export into this store. Existing records are kept; nothing is overwritten.</summary>
    public (int Concepts, int Cards) Import(string json)
    {
        ExportDocument? doc;
        try { doc = JsonSerializer.Deserialize<ExportDocument>(json, ExportJson); }
        catch (JsonException e) { throw new InvalidDataException("This is not a valid Vernier export: " + e.Message, e); }
        if (doc is null || doc.Format != "vernier-learning") throw new InvalidDataException("This is not a Vernier learning export.");
        if (doc.Version > 1) throw new InvalidDataException($"Export version {doc.Version} is newer than this build supports.");

        var ids = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in doc.Concepts ?? [])
        {
            var id = ids[c.Name] = EnsureConcept(c.Subject, c.Name, c.Summary);
            foreach (var p in c.Prerequisites ?? []) AddPrerequisite(id, c.Subject, p);
            if (EvidenceFor(id).Count == 0)
                foreach (var e in c.Evidence ?? []) AddEvidence(id, e.Kind, e.Correct, e.At);
            foreach (var m in c.Misconceptions ?? []) AddMisconception(id, m);
        }
        var cardCount = 0;
        foreach (var k in doc.Cards ?? [])
        {
            if (string.IsNullOrWhiteSpace(k.Front)) continue;
            long? conceptId = k.Concept is not null && ids.TryGetValue(k.Concept, out var cid) ? cid : null;
            var before = Scalar<long>("SELECT COUNT(*) FROM cards");
            var id = AddCard(conceptId, k.Front, k.Back ?? "");
            if (Scalar<long>("SELECT COUNT(*) FROM cards") == before) continue;
            lock (_gate)
                Exec("UPDATE cards SET ease = $e, interval_days = $i, reps = $r, due_at = $d WHERE id = $c", null,
                    ("$e", Math.Max(Sm2.MinEase, k.Ease)), ("$i", Math.Max(0, k.IntervalDays)), ("$r", Math.Max(0, k.Repetitions)), ("$d", Iso(k.Due)), ("$c", id));
            cardCount++;
        }
        foreach (var f in doc.Formulas ?? [])
            if (ids.TryGetValue(f.Concept, out var cid)) AddFormula(cid, f.Expression, f.Meaning, f.Derivation);
        return (ids.Count, cardCount);
    }

    /// <summary>Front/back CSV (RFC 4180), importable by Anki and most flashcard tools.</summary>
    public string ExportCardsCsv()
    {
        var sb = new StringBuilder("front,back,concept\r\n");
        foreach (var c in Cards().Where(c => !c.IsSample))
            sb.Append(Csv(c.Front)).Append(',').Append(Csv(c.Back)).Append(',').Append(Csv(c.Concept)).Append("\r\n");
        return sb.ToString();

        static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    // ---------- sample data ----------

    public bool IsEmpty => Scalar<long>("SELECT (SELECT COUNT(*) FROM concepts) + (SELECT COUNT(*) FROM cards) + (SELECT COUNT(*) FROM sessions)") == 0;

    public void SeedSampleData(DateTimeOffset now)
    {
        const string subject = "Sample · Mechanics";
        var kinematics = EnsureConcept(subject, "Constant-acceleration kinematics", "Motion when acceleration does not change: v = v0 + a·t.", sample: true);
        var newton = EnsureConcept(subject, "Newton's second law", "Net force equals mass times acceleration.", sample: true);
        AddPrerequisite(newton, subject, "Constant-acceleration kinematics");
        Exec("UPDATE concepts SET is_sample = 1 WHERE subject_id = (SELECT id FROM subjects WHERE name = $s)", null, ("$s", subject));

        AddEvidence(kinematics, EvidenceKinds.Diagnostic, true, now.AddDays(-3));
        AddEvidence(kinematics, EvidenceKinds.Explain, true, now.AddDays(-2));
        AddEvidence(newton, EvidenceKinds.Diagnostic, false, now.AddDays(-1));
        AddMisconception(newton, "(Sample) Treats a constant velocity as needing a net force.");
        AddFormula(newton, "F = m·a", "Net force on a body equals its mass times its acceleration.", "Definition of force from the rate of change of momentum, with constant mass: F = d(mv)/dt = m·dv/dt.");
        AddFormula(kinematics, "v = v0 + a·t", "Velocity after time t under constant acceleration.", "Integrate a = dv/dt with a constant.");
        AddExample(newton, "A 2 kg cart accelerates at 3 m/s². What net force acts on it?", "F = m·a = 2 kg × 3 m/s² = 6 N.");
        AddCard(newton, "(Sample) State Newton's second law and say what each symbol means.", "F = m·a: net force (N), mass (kg), acceleration (m/s²).", sample: true, due: now.AddHours(-1));
        AddCard(kinematics, "(Sample) A car goes from rest at 2 m/s² for 5 s. Final speed?", "v = v0 + a·t = 0 + 2×5 = 10 m/s.", sample: true, due: now.AddHours(-1));
        var session = StartSession("(Sample) Why does a cart keep moving?", "Tutor", sample: true);
        AddMessage(session, "user", "If nothing pushes the cart, why doesn't it stop?");
        AddMessage(session, "assistant", "This is sample data. On a frictionless track no net force acts, so by Newton's second law the acceleration is zero and the velocity stays constant.");
    }

    public void RemoveSampleData()
    {
        lock (_gate)
            Exec("DELETE FROM subjects WHERE is_sample = 1; DELETE FROM cards WHERE is_sample = 1; DELETE FROM sessions WHERE is_sample = 1;");
    }

    public bool HasSampleData => Scalar<long>("SELECT (SELECT COUNT(*) FROM subjects WHERE is_sample = 1) + (SELECT COUNT(*) FROM cards WHERE is_sample = 1)") > 0;

    // ---------- episodes (context engine) ----------

    public IReadOnlyList<Context.Episode> LoadEpisodesSince(DateTimeOffset since) =>
        Query("SELECT id, app, key, title, url, started_at, ended_at, active_seconds, media_title, media_position, media_duration, rewinds, questions, last_question, summary FROM episodes WHERE ended_at >= $s ORDER BY started_at",
            r =>
            {
                var e = new Context.Episode
                {
                    Id = r.GetInt64(0), App = r.GetString(1), Key = r.GetString(2), Title = r.GetString(3), Url = r.IsDBNull(4) ? null : r.GetString(4),
                    Start = DateTimeOffset.Parse(r.GetString(5), CultureInfo.InvariantCulture), End = DateTimeOffset.Parse(r.GetString(6), CultureInfo.InvariantCulture),
                    ActiveSeconds = r.GetDouble(7), MediaTitle = r.IsDBNull(8) ? null : r.GetString(8),
                    MediaPosition = r.IsDBNull(9) ? null : TimeSpan.FromSeconds(r.GetDouble(9)), MediaDuration = r.IsDBNull(10) ? null : TimeSpan.FromSeconds(r.GetDouble(10)),
                    Questions = r.GetInt32(12), LastQuestion = r.IsDBNull(13) ? null : r.GetString(13), Summary = r.IsDBNull(14) ? null : r.GetString(14),
                };
                if (!r.IsDBNull(11))
                    foreach (var pair in r.GetString(11).Split(';', StringSplitOptions.RemoveEmptyEntries))
                        if (pair.Split('>') is [var a, var b] && double.TryParse(a, CultureInfo.InvariantCulture, out var from) && double.TryParse(b, CultureInfo.InvariantCulture, out var to))
                            e.Rewinds.Add((TimeSpan.FromSeconds(from), TimeSpan.FromSeconds(to)));
                return e;
            }, ("$s", Iso(since)));

    public void SaveEpisode(Context.Episode e)
    {
        object N(object? v) => v ?? DBNull.Value;
        var rewinds = string.Join(';', e.Rewinds.TakeLast(20).Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.From.TotalSeconds:0}>{r.To.TotalSeconds:0}")));
        (string, object)[] args =
        [
            ("$app", e.App), ("$key", e.Key), ("$title", e.Title), ("$url", N(e.Url)), ("$s", Iso(e.Start)), ("$e", Iso(e.End)), ("$a", e.ActiveSeconds),
            ("$mt", N(e.MediaTitle)), ("$mp", N(e.MediaPosition?.TotalSeconds)), ("$md", N(e.MediaDuration?.TotalSeconds)), ("$rw", rewinds),
            ("$q", e.Questions), ("$lq", N(e.LastQuestion)), ("$sum", N(e.Summary)),
        ];
        lock (_gate)
        {
            if (e.Id == 0)
            {
                Exec("INSERT INTO episodes(app, key, title, url, started_at, ended_at, active_seconds, media_title, media_position, media_duration, rewinds, questions, last_question, summary) " +
                     "VALUES($app, $key, $title, $url, $s, $e, $a, $mt, $mp, $md, $rw, $q, $lq, $sum)", null, args);
                e.Id = Scalar<long>("SELECT last_insert_rowid()");
            }
            else
                Exec("UPDATE episodes SET title=$title, url=$url, ended_at=$e, active_seconds=$a, media_title=$mt, media_position=$mp, media_duration=$md, rewinds=$rw, " +
                     "questions=$q, last_question=$lq, summary=$sum WHERE id=$id", null, [.. args, ("$id", e.Id)]);
        }
    }

    public int DeleteEpisodes(DateTimeOffset? before = null, DateTimeOffset? since = null)
    {
        lock (_gate)
            return before is { } b ? Exec("DELETE FROM episodes WHERE ended_at < $t", null, ("$t", Iso(b)))
                 : since is { } s ? Exec("DELETE FROM episodes WHERE ended_at >= $t", null, ("$t", Iso(s)))
                 : Exec("DELETE FROM episodes");
    }

    // A schema upgrade is the riskiest moment for the file: a copy is taken first.
    private void BackupBeforeMigration(int version)
    {
        try
        {
            var copy = _path + $".before-v{version + 1}";
            if (!File.Exists(copy)) using (var dest = new SqliteConnection($"Data Source={copy};Pooling=False")) { dest.Open(); _db.BackupDatabase(dest); }
        }
        catch (Exception e) when (e is IOException or SqliteException or UnauthorizedAccessException)
        {
            RecoveryNotice ??= "A copy of the learning database could not be made before upgrading it: " + e.Message;
        }
    }

    // ---------- plumbing ----------

    private int Exec(string sql, SqliteTransaction? tx = null, params (string Name, object Value)[] args)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v is bool b ? (b ? 1 : 0) : v);
        return cmd.ExecuteNonQuery();
    }

    private T Scalar<T>(string sql, params (string Name, object Value)[] args)
    {
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v is bool b ? (b ? 1 : 0) : v);
            var result = cmd.ExecuteScalar();
            if (result is null or DBNull) return default!;
            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T)Convert.ChangeType(result, type, CultureInfo.InvariantCulture);
        }
    }

    private List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object Value)[] args)
    {
        lock (_gate)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in args)
                if (sql.Contains(n, StringComparison.Ordinal)) cmd.Parameters.AddWithValue(n, v);
            using var reader = cmd.ExecuteReader();
            var list = new List<T>();
            while (reader.Read()) list.Add(map(reader));
            return list;
        }
    }

    private static string Iso(DateTimeOffset d) => d.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    private static DateTimeOffset Date(string s) => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    private static string Clean(string? s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    public void Dispose()
    {
        lock (_gate) _db.Dispose();
    }
}
