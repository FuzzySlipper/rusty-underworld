using AbyssRpg.Rulesets.UltimaUnderworld.Conversation;
using UltimaUnderworld.Import;
using static AbyssRpg.Rulesets.UltimaUnderworld.Conversation.UuConversationVm;
using Xunit;
using Xunit.Abstractions;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuConversationVmTests
{
    private readonly ITestOutputHelper _output;

    public UuConversationVmTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class StubImports : IUuConversationImports
    {
        public readonly List<string> Calls = [];
        public int Call(string name, UuConversationVm vm)
        {
            Calls.Add(name);
            return 0;
        }
    }

    private sealed class StubVariables : IUuConversationVariables
    {
        private readonly Dictionary<int, int> _values = [];
        public int Read(int address) => _values.TryGetValue(address, out int value) ? value : 0;
        public void Write(int address, int value) => _values[address] = value;
        public int Stored(int address) => _values.TryGetValue(address, out int value) ? value : 0;
    }

    private static ConversationScript Script(params short[] code) =>
        new(0, code.Length, 0, 4, [], code);

    private static UuConversationVm Run(ConversationScript header, StubImports? imports = null, StubVariables? variables = null)
    {
        var vm = new UuConversationVm(
            header,
            (block, index) => $"[{block}:{index}]",
            imports ?? new StubImports(),
            variables ?? new StubVariables());
        vm.Run();
        return vm;
    }

    [Fact]
    public void Arithmetic_jumps_calls_and_say()
    {
        // PUSHI 6, PUSHI 7, ADD, PUSHI 13, TSTEQ, BEQ +2, PUSHI 99, SAY, EXIT, PUSHI 100, SAY, EXIT
        var vm = Run(Script(22, 6, 22, 7, 1, 22, 13, 13, 16, 2, 22, 99, 39, 38, 22, 100, 39, 38));
        Assert.Equal(["[0:99]"], vm.Transcript);
    }

    [Fact]
    public void Calls_return_and_variables_round_trip()
    {
        var variables = new StubVariables();
        // PUSHI 42, PUSHI_EFF 0, STO (mem[4] = 42), PUSHI_EFF 0, FETCHM, PUSHI 42, TSTEQ, BEQ +2, PUSHI 1, SAY, EXIT, PUSHI 0, SAY, EXIT
        var vm = Run(
            Script(23, 0, 22, 42, 32, 23, 0, 31, 22, 42, 13, 16, 2, 22, 1, 39, 38, 22, 0, 39, 38),
            variables: variables);
        Assert.Equal(["[0:1]"], vm.Transcript);
    }

    [Fact]
    public void Imported_calls_dispatch_and_divide_guards()
    {
        var imports = new StubImports();
        var header = new ConversationScript(
            0, 0, 0, 4,
            [new ScriptImport("ask", 0x40, false, 0x129)],
            [22, 10, 22, 0, 4, 20, 0x40, 24, 38]);
        var vm = new UuConversationVm(header, (block, index) => "", imports, new StubVariables());
        vm.Run();
        Assert.Equal(["ask"], imports.Calls);
        // 10/0 guards to 0x7FFF, popped silently.
        Assert.Empty(vm.Transcript);
    }

    [Fact]
    public void Unknown_imports_write_zero_over_the_top()
    {
        // CALLI with an id no import declares: donor still writes 0 over the top.
        var vm = Run(Script(22, 7, 20, 0x41, 22, 0, 13, 16, 2, 22, 1, 39, 38, 22, 0, 39, 38));
        Assert.Equal(["[0:1]"], vm.Transcript); // top became 0, matching PUSHI 0
    }

    [Fact]
    public void Shipped_conversation_runs_to_exit_with_stub_imports()
    {
        byte[]? archive = RequireDataOrSkip("UW/DATA/CNV.ARK");
        byte[]? strings = RequireDataOrSkip("UW/DATA/STRINGS.PAK");
        if (archive is null || strings is null) return;

        CnvArkReader.DialoguePack pack = CnvArkReader.ReadPack(archive, "UW/DATA/CNV.ARK");
        StringsPakReader.DecodedStrings decoded = StringsPakReader.Decode(strings);
        CnvArkReader.ConversationHeader raw = pack.Conversations[0];
        ConversationScript convo = new(
            raw.Index, raw.CodeSize, raw.StringBlock, raw.MemorySlots,
            raw.Imports.Select(i => new ScriptImport(i.Name, i.IdOrAddress, i.IsVariable, i.ReturnType)).ToList(),
            raw.Code);
        string Text(int block, int index) =>
            decoded.Blocks.TryGetValue(block, out var list) && (uint)index < (uint)list.Count
                ? list[index]
                : $"[{block}:{index}]";

        var vm = new UuConversationVm(convo, Text, new StubImports(), new StubVariables())
        {
            StepLimit = 100000,
        };
        vm.Run();
        _output.WriteLine($"Convo {convo.Index}: {vm.Transcript.Count} says, e.g. {vm.Transcript.FirstOrDefault()}");
        Assert.NotEmpty(vm.Transcript);
    }

    // Returns null after writing a SKIP notice when operator data is absent.
    private byte[]? RequireDataOrSkip(string relative)
    {
        string root = FindRepoRoot();
        string path = Path.Combine(root, "local", "extracted", "uw", relative);
        if (!File.Exists(path))
        {
            _output.WriteLine($"SKIP: operator UW1 file missing, nothing checked: {relative}");
            return null;
        }

        return File.ReadAllBytes(path);
    }

    private static string FindRepoRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
