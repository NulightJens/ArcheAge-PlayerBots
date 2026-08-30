namespace AAEmu.UnitTests.Utils.Mocks;

public sealed class FixedHealthCharacterMock : CharacterMock
{
    public int FixedMaxHp { get; set; } = 100;

    public override int MaxHp => FixedMaxHp;
}
