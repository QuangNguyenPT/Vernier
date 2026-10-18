using Vernier.Core.Geometry;

namespace Vernier.Core.Annotations;

public enum AnnotationKind { Arrow, Bracket, Highlight, Circle, Path, Freehand, Label, Underline, Point, Note, Region, Diagram }

/// <summary>
/// A structured mark on the screen. <see cref="Points"/> are virtual-desktop physical pixels:
/// Arrow = from, to; Bracket/Highlight/Circle = two opposite corners of the bounding box;
/// Path/Freehand = polyline; Label = anchor; Note = the card's top-left and bottom-right corners.
/// </summary>
public sealed record Annotation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required AnnotationKind Kind { get; init; }
    public required IReadOnlyList<PxPoint> Points { get; init; }
    public string Color { get; init; } = AnnotationColors.Focus;
    public double StrokeWidth { get; init; } = 2.5;
    public string? Label { get; init; }
    /// <summary>For <see cref="AnnotationKind.Note"/>: what is written on the card.</summary>
    public NoteContent? Note { get; init; }
    /// <summary>For <see cref="AnnotationKind.Diagram"/>: what is drawn (a 3D scene, the complex plane, a hypercube).</summary>
    public Diagrams.DiagramSpec? Diagram { get; init; }
    /// <summary>Why the mark exists, e.g. "the term that was dropped".</summary>
    public string Meaning { get; init; } = "";
    public string? TargetDescription { get; init; }
    /// <summary>Region the mark refers to, used to re-anchor when the target window moves.</summary>
    public PxRect? TargetRegion { get; init; }
    /// <summary>0..1. Model-placed marks are approximate; user-drawn marks are 1.</summary>
    public double Confidence { get; init; } = 1;
    /// <summary>Known lower bound on placement error in physical pixels.</summary>
    public double UncertaintyPx { get; init; }
    public string Source { get; init; } = "user";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan? Lifetime { get; init; }

    public bool IsExpired(DateTimeOffset now) => Lifetime is { } l && now - CreatedAt > l;

    public Annotation Offset(double dx, double dy) => this with
    {
        Points = Points.Select(p => new PxPoint(p.X + dx, p.Y + dy)).ToArray(),
        TargetRegion = TargetRegion is { } r ? r with { X = r.X + dx, Y = r.Y + dy } : null,
    };
}

public static class AnnotationColors
{
    public const string Focus = "#2F9BE6";      // signal blue: the tutor pointing at something
    public const string Learner = "#FFD400";    // marker yellow: ink the learner drew
    public const string Correct = "#2B8A3E";
    public const string Error = "#C92A2A";
    public const string Note = "#1864AB";
    public const string Neutral = "#495057";

    public static string ForMeaning(string? semantic) => semantic?.ToLowerInvariant() switch
    {
        "correct" => Correct,
        "error" or "mistake" => Error,
        "note" or "info" => Note,
        "neutral" => Neutral,
        _ => Focus,
    };
}

/// <summary>Undoable annotation list. Every mutation is one undo step, including Clear.</summary>
public sealed class AnnotationStore
{
    private const int MaxHistory = 200;
    private readonly List<IReadOnlyList<Annotation>> _undo = [];
    private readonly List<IReadOnlyList<Annotation>> _redo = [];
    private IReadOnlyList<Annotation> _items = [];

    public event Action? Changed;

    public IReadOnlyList<Annotation> Items => _items;
    public bool Visible { get; private set; } = true;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Add(params Annotation[] annotations)
    {
        if (annotations.Length == 0) return;
        Commit([.. _items, .. annotations]);
    }

    public void Remove(Guid id) => Commit(_items.Where(a => a.Id != id).ToArray());

    /// <summary>Removes every mark matching the predicate, as one undo step.</summary>
    public void RemoveWhere(Func<Annotation, bool> predicate)
    {
        var kept = _items.Where(a => !predicate(a)).ToArray();
        if (kept.Length != _items.Count) Commit(kept);
    }

    public void Clear()
    {
        if (_items.Count > 0) Commit([]);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Add(_items);
        _items = Pop(_undo);
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Add(_items);
        _items = Pop(_redo);
        Changed?.Invoke();
    }

    public void SetVisible(bool visible)
    {
        if (Visible == visible) return;
        Visible = visible;
        Changed?.Invoke();
    }

    /// <summary>Drops expired marks. Not an undo step: expiry is lifecycle, not an edit.</summary>
    public void PruneExpired(DateTimeOffset now)
    {
        var kept = _items.Where(a => !a.IsExpired(now)).ToArray();
        if (kept.Length == _items.Count) return;
        _items = kept;
        Changed?.Invoke();
    }

    /// <summary>
    /// Re-anchors marks whose target lay inside a window that moved. Marks whose target would
    /// leave the desktop are dropped rather than drawn in a wrong place.
    /// </summary>
    /// <param name="belongs">Marks that belong to the window wherever they sit (a card beside it on free space): they move too.</param>
    public void Rebase(PxRect oldBounds, PxRect newBounds, PxRect desktop, Func<Annotation, bool>? belongs = null)
    {
        var dx = newBounds.X - oldBounds.X;
        var dy = newBounds.Y - oldBounds.Y;
        if (dx == 0 && dy == 0) return;
        var result = new List<Annotation>();
        foreach (var a in _items)
        {
            var anchored = belongs?.Invoke(a) == true || (a.TargetRegion is { } t ? oldBounds.Contains(t) : a.Points.All(oldBounds.Contains));
            if (!anchored) { result.Add(a); continue; }
            var moved = a.Offset(dx, dy);
            if (moved.Points.All(desktop.Contains)) result.Add(moved);
        }
        _items = result;
        Changed?.Invoke();
    }

    /// <summary>Moves single marks (their content scrolled or reflowed, see <see cref="MarkRefit"/>). Not an undo step: it follows the screen, it is not an edit.</summary>
    public void Shift(IReadOnlyDictionary<Guid, PxPoint> moves)
    {
        if (moves.Count == 0) return;
        _items = _items.Select(a => moves.TryGetValue(a.Id, out var d) ? a.Offset(d.X, d.Y) : a).ToArray();
        Changed?.Invoke();
    }

    /// <summary>Drops marks that fall outside every monitor after a display change.</summary>
    public void DropOutside(IReadOnlyList<MonitorInfo> monitors)
    {
        var kept = _items.Where(a => a.Points.All(p => monitors.Any(m => m.BoundsPhysical.Contains(p)))).ToArray();
        if (kept.Length == _items.Count) return;
        _items = kept;
        Changed?.Invoke();
    }

    private void Commit(IReadOnlyList<Annotation> next)
    {
        _undo.Add(_items);
        if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
        _redo.Clear();
        _items = next;
        Changed?.Invoke();
    }

    private static IReadOnlyList<Annotation> Pop(List<IReadOnlyList<Annotation>> stack)
    {
        var last = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return last;
    }
}
