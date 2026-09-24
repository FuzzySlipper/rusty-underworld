// Source-file identities for the UW1 tables this importer reads.
// All paths are relative to an operator-supplied UW1 install root
// (the UW/ tree of the game ISO). UW2 paths are never listed here.
namespace UltimaUnderworld.Import;

public static class Uw1TablePaths
{
    public const string StringsPak = "UW/DATA/STRINGS.PAK";
    public const string ObjectsDat = "UW/DATA/OBJECTS.DAT";
    public const string CommonObjDat = "UW/DATA/COMOBJ.DAT";
    public const string SkillsDat = "UW/DATA/SKILLS.DAT";
    public const string ChargenDat = "UW/DATA/CHRGEN.DAT";
}
