namespace AAEmu.Game.Core.Managers.Bots;

public static class BotSkillIds
{
    public static class Abolisher
    {
        public static IReadOnlyList<uint> SkillLearnOrder { get; } = new uint[]
        {
            18132, 10399, 16486, 11918, 10645, 13282, 10501, 12048, 10644, 12039,
            11365, 12046, 10375, 11991, 12034, 10377, 10436, 11429, 11380, 10372
        };
    }

    public static class Darkrunner
    {
        public static IReadOnlyList<uint> SkillLearnOrder { get; } = new uint[]
        {
            18131, 18132, 18134, 16486, 11918, 10648, 12049, 13282,
            10644, 12029, 10152, 10082, 12034, 12026, 13344, 10377,
            13315, 10496, 10189, 12075, 11429, 10104, 11380
        };
    }

    public static class Reaper
    {
        public static IReadOnlyList<uint> SkillLearnOrder { get; } = new uint[]
        {
            10752, 10667, 10201, 10153, 12049, 10135, 10670, 10151, 11395, 10082,
            11314, 12796, 11967, 11939, 10189, 12759, 10664, 14774, 12075
        };
    }

    public static class Daggerspell
    {
        public static IReadOnlyList<uint> SkillLearnOrder { get; } = new uint[]
        {
            10752, 14376, 10667, 10159, 10153, 10154, 10670, 12049, 12001,
            10082, 12796, 10134, 11967, 11353, 10664, 12075, 11443
        };
    }

    public static class Templar
    {
        public static IReadOnlyList<uint> SkillLearnOrder { get; } = new uint[]
        {
            10534, 11379, 10547, 10399, 10645, 13286, 11365, 10720, 16486, 16783,
            12046, 10152, 16004, 17412, 10375, 10436, 14929, 10372
        };
    }

    public static class Cleric
    {
        public static IReadOnlyList<uint> SkillLearnOrder { get; } = new uint[]
        {
            10534, 16486, 11379, 18222, 11973, 11869, 11934, 10547, 10710, 11943,
            10546, 10152, 17413, 13286, 11991, 11377, 10720, 11989, 10724, 16783,
            11429, 11948, 16004, 11380, 11396, 17412, 10104, 10727, 14929, 10714,
            11961, 10721
        };
    }

    public static class Primeval
    {
        public static IReadOnlyList<uint> SkillLearnOrder { get; } = new uint[]
        {
            16210, 13564, 14835, 11368, 16486, 15073, 10648, 12049,
            12133, 10152, 10694, 12139, 10082, 11429, 14760, 11933,
            13281, 10104, 10189, 10481
        };
    }
}
