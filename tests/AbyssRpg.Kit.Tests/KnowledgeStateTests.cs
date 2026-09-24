using AbyssRpg.Kit.Knowledge;
using Xunit;

namespace AbyssRpg.Kit.Tests;

public sealed class KnowledgeStateTests
{
    [Fact]
    public void Automap_reveals_and_round_trips_rle()
    {
        var page = new AutomapPage();
        Assert.False(page.IsMapped(10, 10));
        page.Reveal(10, 10);
        Assert.True(page.IsMapped(10, 10));
        Assert.False(page.IsMapped(64, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.Reveal(64, 0));

        page.RevealDisc(20, 20, 1);
        Assert.Equal(1 + 5, page.MappedCount); // center + 4 orthogonal

        string encoded = page.EncodePage();
        var revived = new AutomapPage();
        revived.DecodeInto(encoded);
        Assert.Equal(page.MappedCount, revived.MappedCount);
        Assert.True(revived.IsMapped(10, 10));

        Assert.Equal("0:100,5,50", AutomapPage.Encode(Enumerable.Repeat(false, 100).Concat(Enumerable.Repeat(true, 5)).Concat(Enumerable.Repeat(false, 50)).ToArray()));
        Assert.Throws<ArgumentException>(() => AutomapPage.Decode("bogus", 10));
        Assert.Throws<ArgumentException>(() => AutomapPage.Decode("0:5", 10));
    }

    [Fact]
    public void Notes_place_edit_delete_per_level()
    {
        var notes = new QuillNotes();
        Assert.Empty(notes.Notes(1));
        int index = notes.Place(1, "trap here", 10, 12);
        Assert.Equal(0, index);
        notes.Place(2, "other level", 0, 0);
        Assert.Single(notes.Notes(1));
        Assert.Single(notes.Notes(2));

        notes.Edit(1, 0, "trap disarmed");
        Assert.Equal("trap disarmed", notes.Notes(1)[0].Text);
        notes.Delete(1, 0);
        Assert.Empty(notes.Notes(1));
        Assert.Single(notes.Notes(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => notes.Edit(1, 0, "x"));
        Assert.Throws<ArgumentException>(() => notes.Place(1, "", 0, 0));
    }

    [Fact]
    public void Quest_variables_read_and_write()
    {
        var vars = new QuestVariables();
        Assert.Equal(0, vars.Get(0));
        vars.Set(0, 2);
        vars.Set(37, 5);
        Assert.Equal(2, vars.Get(0));
        Assert.Equal(5, vars.Get(37));
        Assert.Throws<ArgumentOutOfRangeException>(() => vars.Get(64));
        Assert.Throws<ArgumentOutOfRangeException>(() => vars.Set(-1, 1));
    }
}
