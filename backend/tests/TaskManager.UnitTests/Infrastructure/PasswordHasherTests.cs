using TaskManager.Infrastructure.Security;

namespace TaskManager.UnitTests.Infrastructure;

public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void Hash_NeverReturnsThePlainPassword_AndIsSalted()
    {
        var first = _sut.Hash("Sup3rSecret!");
        var second = _sut.Hash("Sup3rSecret!");

        Assert.DoesNotContain("Sup3rSecret!", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var hash = _sut.Hash("Sup3rSecret!");

        Assert.True(_sut.Verify(hash, "Sup3rSecret!"));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("Sup3rSecret!");

        Assert.False(_sut.Verify(hash, "sup3rsecret!"));
    }

    [Fact]
    public void Verify_WithNullHash_ReturnsFalse()
    {
        Assert.False(_sut.Verify(null, "Sup3rSecret!"));
    }

    [Fact]
    public void Verify_WithCorruptedHash_ReturnsFalseInsteadOfThrowing()
    {
        Assert.False(_sut.Verify("not-a-valid-hash", "Sup3rSecret!"));
    }
}
