# Data formats

## Learning export (`vernier-learning`, version 1)

Settings › Your data › **Export learning record…** writes UTF-8 JSON. **Import…** merges such a file: existing concepts, evidence and cards are kept, nothing is overwritten, importing twice adds nothing. Sample data is never exported. Files with another `format` or a higher `version` are rejected.

```json
{
  "format": "vernier-learning",
  "version": 1,
  "exportedAt": "2026-06-01T12:00:00+00:00",
  "concepts": [
    {
      "subject": "Maths",
      "name": "Chain rule",
      "summary": "d/dx f(g(x)) = f'(g(x))·g'(x)",
      "prerequisites": ["Derivative"],
      "evidence": [{ "kind": "explain", "correct": true, "at": "2026-06-01T12:00:00+00:00" }],
      "misconceptions": ["forgets the inner derivative"]
    }
  ],
  "cards": [
    { "concept": "Chain rule", "front": "State the chain rule", "back": "f'(g(x))·g'(x)",
      "ease": 2.5, "intervalDays": 3, "repetitions": 2, "due": "2026-06-04T12:00:00+00:00" }
  ],
  "formulas": [{ "id": 1, "concept": "Chain rule", "expression": "(f∘g)' = (f'∘g)·g'", "meaning": "…", "derivation": "…" }],
  "examples": [{ "id": 1, "concept": "Chain rule", "problem": "…", "solution": "…" }]
}
```

`evidence.kind` is one of `diagnostic`, `explain`, `transfer`, `recall`, `self_report` (recorded but never counted toward mastery). Sessions (conversation text) are not part of the export; save a session to an Obsidian vault from Library › Sessions instead.

## Cards CSV

**Export cards as CSV…** writes RFC 4180 CSV with a header row `front,back,concept`, every field quoted, CRLF line ends. It imports into Anki (File › Import, comma separator, first field = front) and most flashcard tools. Scheduling state is not included.

## Obsidian notes

Written only under `<vault>/Vernier/`:

- `Concepts/<Concept>.md`: front matter (`source: vernier`, `subject`, `updated`), summary, key formula, `[[wikilinks]]` to prerequisites and subject, worked example, mistake to watch. If the note exists, a dated `## Update` section is appended.
- `Sessions/<yyyy-MM-dd> <Title>.md`: the conversation.
- `Mistake log.md`: one appended line per mistake, linking the concept.

## Database

`learning.db` is SQLite, schema version in `PRAGMA user_version` (currently 2). Tables: `subjects`, `concepts`, `concept_prereqs`, `sessions`, `messages` (+ FTS5 `messages_fts`), `evidence`, `misconceptions`, `cards`, `reviews`, `formulas`, `examples`, `open_questions`. Timestamps are ISO-8601 UTC text. Treat the file as private to Vernier; use the export for interchange.
