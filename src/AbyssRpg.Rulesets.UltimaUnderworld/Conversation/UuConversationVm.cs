namespace AbyssRpg.Rulesets.UltimaUnderworld.Conversation;

/// <summary>
/// Imported-function host for the conversation VM. Implementations PEEK
/// their arguments (At/Pop-free reads) and return the result; the VM writes
/// it over the stack top (defaulting 0, including unknown imports),
/// matching the donor result_register writeback.
/// </summary>
public interface IUuConversationImports
{
    int Call(string name, UuConversationVm vm);
}

/// <summary>
/// Imported-variable backing (bglobals + quest flags): preloaded into VM
/// memory at start, written back at exit.
/// </summary>
public interface IUuConversationVariables
{
    int Read(int address);
    void Write(int address, int value);
}

/// <summary>
/// Ruleset-owned CNV interpretation: the 42-opcode stack machine over a
/// normalized conversation (T26 records). Semantics follow the donor VM
/// (stack/base-pointer frames, in-place memory ops, absolute JMP/CALL,
/// relative BEQ/BNE/BRA, SAY collecting output, CALLI dispatching to the
/// import host). STRCMP (37) is a documented no-op — declared but never
/// implemented in the donor either. Written from the behavior, not the code.
/// </summary>
public sealed class UuConversationVm
{
    public const int MemorySize = 4096;
    public const int DivideByZeroResult = 0x7FFF;

    private readonly short[] _memory = new short[MemorySize];
    private readonly ConversationScript _conversation;
    private readonly Func<int, int, string> _strings;
    private readonly IUuConversationImports _imports;
    private readonly IUuConversationVariables _variables;
    private readonly List<string> _transcript = [];

    private int _stackBase;
    private int _stackPtr;
    private int _basePtr;
    private int _result;
    private int _callLevel;

    public IReadOnlyList<string> Transcript => _transcript;

    /// <summary>
    /// Product safety cap on executed instructions (0 = unbounded, the
    /// donor behavior). Tests and the host set a budget; the donor has none.
    /// </summary>
    public int StepLimit { get; set; }

    public sealed record ScriptImport(string Name, int IdOrAddress, bool IsVariable, int ReturnType);

    public sealed record ConversationScript(
        int Index,
        int CodeSize,
        int StringBlock,
        int MemorySlots,
        IReadOnlyList<ScriptImport> Imports,
        short[] Code);

    public UuConversationVm(
        ConversationScript conversation,
        Func<int, int, string> strings,
        IUuConversationImports imports,
        IUuConversationVariables variables)
    {
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
        _imports = imports ?? throw new ArgumentNullException(nameof(imports));
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
    }

    public short At(int index) => (uint)index < MemorySize ? _memory[index] : (short)0;

    public void Push(int value)
    {
        _stackPtr++;
        _memory[_stackBase + _stackPtr] = (short)value;
    }

    /// <summary>Peek a call argument without popping (depth 0 = top).</summary>
    public short PeekArg(int depth) => At(_stackBase + _stackPtr - depth);

    public short Pop()
    {
        // Lenient underflow (0 via At) where the donor throws: only
        // reachable by popping deeper than pushed (malformed scripts).
        short value = At(_stackBase + _stackPtr);
        _stackPtr--;
        return value;
    }

    public void Run()
    {
        _stackBase = _conversation.MemorySlots;
        _stackPtr = 0;
        _basePtr = 0;
        _result = 0;
        _callLevel = 1;

        foreach (ScriptImport import in _conversation.Imports)
            if (import.IsVariable)
                _memory[import.IdOrAddress] = (short)_variables.Read(import.IdOrAddress);

        short[] code = _conversation.Code;
        bool finished = false;
        int ip = 0;
        int steps = 0;
        while (!finished)
        {
            if (StepLimit > 0 && steps++ >= StepLimit)
                throw new InvalidOperationException("Conversation step limit exceeded.");
            short op = code[ip];
            switch (op)
            {
                case 0: break; // NOP
                case 1: { int a = Pop(); int b = Pop(); Push(a + b); break; }
                case 2: { int a = Pop(); int b = Pop(); Push(a * b); break; }
                case 3: { int a = Pop(); int b = Pop(); Push(b - a); break; }
                case 4:
                    if (At(_stackBase + _stackPtr) == 0) Push(DivideByZeroResult);
                    else { int a = Pop(); int b = Pop(); Push(b / a); }
                    break;
                case 5: { int a = Pop(); int b = Pop(); Push(b % a); break; }
                case 6: { int a = Pop(); int b = Pop(); Push(a | b); break; }
                case 7: { int a = Pop(); int b = Pop(); Push(a & b); break; }
                case 8: Push(Pop() == 0 ? 1 : 0); break;
                case 9: { int a = Pop(); int b = Pop(); Push(b > a ? 1 : 0); break; }
                case 10: { int a = Pop(); int b = Pop(); Push(b >= a ? 1 : 0); break; }
                case 11: { int a = Pop(); int b = Pop(); Push(b < a ? 1 : 0); break; }
                case 12: { int a = Pop(); int b = Pop(); Push(b <= a ? 1 : 0); break; }
                case 13: { int a = Pop(); int b = Pop(); Push(b == a ? 1 : 0); break; }
                case 14: { int a = Pop(); int b = Pop(); Push(b != a ? 1 : 0); break; }
                case 15: ip = code[ip + 1] - 1; break; // JMP absolute
                case 16: // BEQ relative
                    if (Pop() == 0) ip += code[ip + 1];
                    else ip++;
                    break;
                case 17: // BNE relative
                    if (Pop() != 0) ip += code[ip + 1];
                    else ip++;
                    break;
                case 18: ip += code[ip + 1]; break; // BRA relative
                case 19: // CALL absolute
                    Push(ip + 1);
                    ip = code[ip + 1] - 1;
                    _callLevel++;
                    break;
                case 20: // CALLI imported (donor writeback: result over stack top, default 0)
                {
                    int id = code[++ip];
                    int result = 0;
                    foreach (ScriptImport import in _conversation.Imports)
                        if (!import.IsVariable && import.IdOrAddress == id)
                        {
                            result = _imports.Call(import.Name, this);
                            break;
                        }

                    _memory[_stackBase + _stackPtr] = (short)result;
                    break;
                }
                case 21: // RET
                    if (--_callLevel < 0) finished = true;
                    else ip = Pop();
                    break;
                case 22: Push(code[++ip]); break; // PUSHI
                case 23: // PUSHI_EFF
                    Push(_conversation.MemorySlots + _basePtr + code[ip + 1]);
                    ip++;
                    break;
                case 24: Pop(); break; // POP
                case 25: { int a = Pop(); int b = Pop(); Push(a); Push(b); break; } // SWAP
                case 26: _stackPtr++; _memory[_stackBase + _stackPtr] = (short)_basePtr; break; // PUSHBP
                case 27: _basePtr = Pop(); break; // POPBP
                case 28: _basePtr = _stackPtr; break; // SPTOBP
                case 29: _stackPtr = _basePtr; break; // BPTOSP
                case 30: _stackPtr += At(_stackBase + _stackPtr) - 1; break; // ADDSP
                case 31: _memory[_stackBase + _stackPtr] = At(At(_stackBase + _stackPtr)); break; // FETCHM
                case 32: // STO (donor writes unchecked; invalid addresses throw)
                {
                    int value = At(_stackBase + _stackPtr);
                    int address = At(_stackBase + _stackPtr - 1);
                    _memory[address] = (short)value;
                    _stackPtr -= 2;
                    break;
                }
                case 33: // OFFSET
                {
                    int a = At(_stackBase + _stackPtr);
                    int b = At(_stackBase + _stackPtr - 1);
                    _stackPtr--;
                    _memory[_stackBase + _stackPtr] = (short)(a + b - 1);
                    break;
                }
                case 34: break; // START
                case 35: _result = Pop(); break; // SAVE_REG
                case 36: Push(_result); break; // PUSH_REG
                case 37: break; // STRCMP: declared, never implemented (donor-faithful no-op)
                case 38: finished = true; break; // EXIT_OP
                case 39: _transcript.Add(_strings(_conversation.StringBlock, Pop())); break; // SAY_OP
                case 40: break; // RESPOND_OP: UI turn-taking, no VM effect
                case 41: Push(-Pop()); break; // OPNEG
                default: break; // unknown opcode: skip (donor behavior)
            }

            ip++;
            if (ip < 0 || ip > code.Length - 1) finished = true;
        }

        foreach (ScriptImport import in _conversation.Imports)
            if (import.IsVariable)
                _variables.Write(import.IdOrAddress, _memory[import.IdOrAddress]);
    }
}
