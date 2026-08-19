namespace Accountant;

public static class Offsets
{
    public static class Submersible
    {
        public const int TimerSize        = 0x24;
        public const int TimerTimeStamp   = 0x00;
        public const int TimerRawName     = 0x08;
        public const int TimerRawNameSize = 0x10;

        public const int StatusSize        = 0x3C;
        public const int StatusTimeStamp   = 0x08;
        public const int StatusRawName     = 0x16;
        public const int StatusRawNameSize = 0x10;
    }

    public static class Airship
    {
        public const int TimerSize        = 0x24;
        public const int TimerTimeStamp   = 0x00;
        public const int TimerRawName     = 0x06;
        public const int TimerRawNameSize = 0x10;

        public const int StatusSize        = 0x24;
        public const int StatusTimeStamp   = 0x08;
        public const int StatusRawName     = 0x10;
        public const int StatusRawNameSize = 0x10;
    }

    public static class Squadrons
    {
        public const int MissionEnd  = 0x00;
        public const int TrainingEnd = 0x04;
        public const int MissionId   = 0x08;
        public const int TrainingId  = 0x0A;
        public const int NewRecruits = 0x0C;
    }

    public static class FreeCompany
    {
        public const int FreeCompanyModuleVfunc = 34;
        public const int DataOffset             = 0x19E0;
    }
}

public static class Signatures
{
    public const string GoldSaucerData = "48 89 5C 24 ?? 57 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 48 8B 0D ?? ?? ?? ?? 48 8B FA";
    public const string SquadronContainer = "8B 3D ?? ?? ?? ?? 8B D8 3B F8";
    public const string PositionInfo = "48 8B 05 ?? ?? ?? ?? 0F 83";


    // 🔴 台服 7.20:上游原本的短特徵碼(結尾停在 "48 8B 49 40")在台服執行檔有 2 個命中,
    //    Dalamud 的 ScanText 取位址最低的那個 = 0x140450BE0 —— 那是一個 xref 全 0 的死函式
    //    (大小 0x49、複製大小 0x80、不含 4×TimerSize 的配置),於是 AirshipTimersDetour
    //    掛得上去卻永遠不會觸發,飛空艇歸還時間靜默永不更新。
    //    正解是第二命中 0x140C88710(大小 0xC6):free/malloc/movups 全是 0x90 = 4 × Airship.TimerSize(0x24),
    //    由 0x140A86233 尾呼叫進來,並與下面潛水艇的 0x140C88980 成對(兩者唯一差別是 +0x40 / +0x48)。
    //    因此把特徵碼延長到 "48 85 C9 74 ?? BA 90 00 00 00"(即 mov edx, 0x90)——全映像唯一命中正解。
    //    je 的位移用 ?? 遮罩,不寫死。
    public const string AirshipTimers = "48 89 5C 24 ?? 57 48 83 EC 20 48 8B D9 48 8B FA 48 8B 49 40 48 85 C9 74 ?? BA 90 00 00 00";
    public const string AirshipStatus = "48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 54 41 56 41 57 48 83 EC ?? 48 8D B1";

    // 🔴 同上:短特徵碼在台服也是 2 命中(0x140C88980 與 0x1413E5F10)。第一命中碰巧就是正解,
    //    但那是運氣不是設計 —— 只要台服下次改版讓那個無關函式排到前面,潛水艇歸還時間就會靜默失效。
    //    一併延長到 mov edx, 0x90,唯一命中 0x140C88980(由 0x140A86403 尾呼叫)。
    public const string SubmersibleTimers = "48 89 5C 24 ?? 57 48 83 EC 20 48 8B D9 48 8B FA 48 8B 49 48 48 85 C9 74 ?? BA 90 00 00 00";
    public const string SubmersibleStatus = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 0F 10 02 4C 8D 81";
}