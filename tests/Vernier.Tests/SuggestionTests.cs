using Vernier.Core.Context;
using Xunit;

namespace Vernier.Tests;

public class SuggestionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void At_most_two_an_hour_and_fifteen_minutes_apart()
    {
        var policy = new SuggestionPolicy();
        Assert.True(policy.TryOffer("rewind", T0));
        Assert.False(policy.TryOffer("other", T0.AddMinutes(10)));  // too soon after the last
        Assert.True(policy.TryOffer("other", T0.AddMinutes(16)));
        Assert.False(policy.TryOffer("third", T0.AddMinutes(40)));  // budget of two spent this hour
        Assert.True(policy.TryOffer("third", T0.AddMinutes(61)));   // the first has aged out
    }

    [Fact]
    public void Each_dismissal_doubles_the_wait_for_that_kind_and_accepting_resets_it()
    {
        var policy = new SuggestionPolicy();
        policy.Dismissed("rewind", T0);
        Assert.False(policy.TryOffer("rewind", T0.AddMinutes(19)));
        Assert.True(policy.TryOffer("rewind", T0.AddMinutes(20)));

        var t1 = T0.AddHours(2);
        policy.Dismissed("rewind", t1); // second dismissal: 40 minutes
        Assert.False(policy.TryOffer("rewind", t1.AddMinutes(39)));
        Assert.True(policy.TryOffer("rewind", t1.AddMinutes(40)));

        var t2 = T0.AddHours(4);
        for (var i = 0; i < 30; i++) policy.Dismissed("rewind", t2); // capped at a day, no overflow
        Assert.False(policy.TryOffer("rewind", t2.AddHours(23)));
        Assert.True(policy.TryOffer("rewind", t2.AddDays(1)));

        policy.Dismissed("rewind", T0.AddDays(3));
        policy.Accepted("rewind");
        Assert.True(policy.TryOffer("rewind", T0.AddDays(3).AddMinutes(1)));
    }

    [Fact]
    public void Rewinding_to_the_same_part_twice_in_five_minutes_offers_help_once()
    {
        var watch = new RewindWatch();
        Assert.Null(watch.Rewound("Lecture 4", TimeSpan.FromSeconds(190), T0));
        Assert.Null(watch.Rewound("Other video", TimeSpan.FromSeconds(195), T0.AddSeconds(30))); // another video does not count
        var s = watch.Rewound("Lecture 4", TimeSpan.FromSeconds(200), T0.AddMinutes(2));
        Assert.NotNull(s);
        Assert.Equal("rewind", s.Kind);
        Assert.Contains("3:10", s.Question);
        Assert.Contains("Lecture 4", s.Question);
        Assert.Null(watch.Rewound("Lecture 4", TimeSpan.FromSeconds(200), T0.AddMinutes(3))); // starts counting again

        var far = new RewindWatch();
        Assert.Null(far.Rewound("Lecture 4", TimeSpan.FromSeconds(60), T0));
        Assert.Null(far.Rewound("Lecture 4", TimeSpan.FromSeconds(400), T0.AddMinutes(1))); // a different part
        Assert.Null(far.Rewound("Lecture 4", TimeSpan.FromSeconds(62), T0.AddMinutes(6)));  // the first is too old
    }
}
