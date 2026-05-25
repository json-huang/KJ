using FluentAssertions;
using KJ.Domain;
using KJ.Drivers.Abstractions;
using Xunit;

namespace KJ.Drivers.Tests;

public class ModbusHelpersTests
{
    // ── ParseAddress ──────────────────────────────────────────────────────

    [Fact]
    public void ParseAddress_HoldingRegister()
    {
        var result = ModbusHelpers.ParseAddress("HR100");
        result.Type.Should().Be(ModbusRegisterType.HoldingRegister);
        result.Address.Should().Be(100);
        result.SlaveId.Should().Be(1);
    }

    [Fact]
    public void ParseAddress_InputRegister()
    {
        var result = ModbusHelpers.ParseAddress("IR50");
        result.Type.Should().Be(ModbusRegisterType.InputRegister);
        result.Address.Should().Be(50);
    }

    [Fact]
    public void ParseAddress_Coil()
    {
        var result = ModbusHelpers.ParseAddress("C10");
        result.Type.Should().Be(ModbusRegisterType.Coil);
        result.Address.Should().Be(10);
    }

    [Fact]
    public void ParseAddress_DiscreteInput()
    {
        var result = ModbusHelpers.ParseAddress("DI5");
        result.Type.Should().Be(ModbusRegisterType.DiscreteInput);
        result.Address.Should().Be(5);
    }

    [Fact]
    public void ParseAddress_WithSlaveId()
    {
        var result = ModbusHelpers.ParseAddress("2:HR100");
        result.Type.Should().Be(ModbusRegisterType.HoldingRegister);
        result.Address.Should().Be(100);
        result.SlaveId.Should().Be(2);
    }

    [Fact]
    public void ParseAddress_CaseInsensitive()
    {
        var r1 = ModbusHelpers.ParseAddress("hr100");
        var r2 = ModbusHelpers.ParseAddress("Hr100");
        var r3 = ModbusHelpers.ParseAddress("hR100");

        r1.Type.Should().Be(ModbusRegisterType.HoldingRegister);
        r2.Type.Should().Be(ModbusRegisterType.HoldingRegister);
        r3.Type.Should().Be(ModbusRegisterType.HoldingRegister);
    }

    [Fact]
    public void ParseAddress_Empty_Throws()
    {
        var act = () => ModbusHelpers.ParseAddress("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ParseAddress_Null_Throws()
    {
        var act = () => ModbusHelpers.ParseAddress(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ParseAddress_Whitespace_Throws()
    {
        var act = () => ModbusHelpers.ParseAddress("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ParseAddress_NoPrefix_Throws()
    {
        var act = () => ModbusHelpers.ParseAddress("123");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseAddress_NoNumber_Throws()
    {
        var act = () => ModbusHelpers.ParseAddress("HR");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseAddress_UnknownPrefix_Throws()
    {
        var act = () => ModbusHelpers.ParseAddress("XX100");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseAddress_LargeAddress()
    {
        var result = ModbusHelpers.ParseAddress("HR65535");
        result.Address.Should().Be(65535);
    }

    [Fact]
    public void ParseAddress_SlaveIdZero()
    {
        var result = ModbusHelpers.ParseAddress("0:HR10");
        result.SlaveId.Should().Be(0);
        result.Address.Should().Be(10);
    }

    // ── ConvertRegistersToValue ───────────────────────────────────────────

    [Fact]
    public void ConvertRegisters_Bool_True()
    {
        var result = ModbusHelpers.ConvertRegistersToValue(new ushort[] { 1 }, TagValueType.Bool);
        result.Should().Be(true);
    }

    [Fact]
    public void ConvertRegisters_Bool_False()
    {
        var result = ModbusHelpers.ConvertRegistersToValue(new ushort[] { 0 }, TagValueType.Bool);
        result.Should().Be(false);
    }

    [Fact]
    public void ConvertRegisters_Int32()
    {
        var result = ModbusHelpers.ConvertRegistersToValue(new ushort[] { 42 }, TagValueType.Int32);
        result.Should().Be(42);
    }

    [Fact]
    public void ConvertRegisters_Int64()
    {
        var result = ModbusHelpers.ConvertRegistersToValue(new ushort[] { 1, 2 }, TagValueType.Int64);
        result.Should().Be((1L << 16) | 2L);
    }

    [Fact]
    public void ConvertRegisters_Int64_Single()
    {
        var result = ModbusHelpers.ConvertRegistersToValue(new ushort[] { 42 }, TagValueType.Int64);
        result.Should().Be(42L);
    }

    [Fact]
    public void ConvertRegisters_Float()
    {
        // IEEE 754 float for 3.14: 0x4048F5C3
        // Big-endian registers: [0x4048, 0xF5C3]
        var result = ModbusHelpers.ConvertRegistersToValue(new ushort[] { 0x4048, 0xF5C3 }, TagValueType.Float);
        ((float)result!).Should().BeApproximately(3.14f, 0.001f);
    }

    [Fact]
    public void ConvertRegisters_String()
    {
        // "Hi" = 0x4869 → register [0x4869]
        var result = ModbusHelpers.ConvertRegistersToValue(new ushort[] { 0x4869 }, TagValueType.String);
        result.Should().Be("Hi");
    }

    [Fact]
    public void ConvertRegisters_Bytes()
    {
        var result = ModbusHelpers.ConvertRegistersToValue(new ushort[] { 0xABCD }, TagValueType.Bytes);
        result.Should().BeEquivalentTo(new byte[] { 0xAB, 0xCD });
    }

    [Fact]
    public void ConvertRegisters_Empty_ReturnsNull()
    {
        var result = ModbusHelpers.ConvertRegistersToValue(Array.Empty<ushort>(), TagValueType.Int32);
        result.Should().BeNull();
    }

    // ── ConvertValueToRegisters ───────────────────────────────────────────

    [Fact]
    public void ConvertValue_Bool_True()
    {
        var result = ModbusHelpers.ConvertValueToRegisters(true, TagValueType.Bool);
        result.Should().Equal(new ushort[] { 1 });
    }

    [Fact]
    public void ConvertValue_Bool_False()
    {
        var result = ModbusHelpers.ConvertValueToRegisters(false, TagValueType.Bool);
        result.Should().Equal(new ushort[] { 0 });
    }

    [Fact]
    public void ConvertValue_Int32()
    {
        var result = ModbusHelpers.ConvertValueToRegisters(42, TagValueType.Int32);
        result.Should().Equal(new ushort[] { 42 });
    }

    [Fact]
    public void ConvertValue_Float()
    {
        var result = ModbusHelpers.ConvertValueToRegisters(3.14f, TagValueType.Float);
        result.Should().HaveCount(2);
        // Round-trip check
        var back = ModbusHelpers.ConvertRegistersToValue(result, TagValueType.Float);
        ((float)back!).Should().BeApproximately(3.14f, 0.001f);
    }

    [Fact]
    public void ConvertValue_Double()
    {
        var result = ModbusHelpers.ConvertValueToRegisters(3.14159265358979, TagValueType.Double);
        result.Should().HaveCount(4);
        var back = ModbusHelpers.ConvertRegistersToValue(result, TagValueType.Double);
        ((double)back!).Should().BeApproximately(3.14159265358979, 0.000001);
    }

    [Fact]
    public void ConvertValue_String()
    {
        var result = ModbusHelpers.ConvertValueToRegisters("Hi", TagValueType.String);
        result.Should().HaveCount(1);
        result[0].Should().Be(0x4869);
    }

    // ── Round-trip ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TagValueType.Bool, true)]
    [InlineData(TagValueType.Int32, 12345)]
    [InlineData(TagValueType.Int64, 100000L)]
    public void RoundTrip_ShouldPreserveValue(TagValueType type, object value)
    {
        var registers = ModbusHelpers.ConvertValueToRegisters(value, type);
        var result = ModbusHelpers.ConvertRegistersToValue(registers, type);
        result.Should().Be(value);
    }

    // ── Internal type visibility ──────────────────────────────────────────

    [Fact]
    public void ModbusRegisterType_ShouldBeAccessible()
    {
        // Verify InternalsVisibleTo works
        var values = Enum.GetValues<ModbusRegisterType>();
        values.Should().HaveCount(4);
    }
}
