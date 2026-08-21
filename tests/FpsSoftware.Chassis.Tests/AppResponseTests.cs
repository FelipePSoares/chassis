using FluentAssertions;

namespace FpsSoftware.Chassis.Tests;

public class AppResponseTests
{
    [Fact]
    public void Success_ShouldSucceedWithoutMessages()
    {
        var response = AppResponse.Success();

        response.Succeeded.Should().BeTrue();
        response.Failed.Should().BeFalse();
        response.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Error_WithDescription_ShouldFailWithGeneralCode()
    {
        var response = AppResponse.Error("something went wrong");

        response.Succeeded.Should().BeFalse();
        response.Failed.Should().BeTrue();
        response.Messages.Should().ContainSingle()
            .Which.Should().Match<AppMessage>(m => m.Code == "general" && m.Description == "something went wrong");
    }

    [Fact]
    public void Error_WithCodeAndDescription_ShouldKeepCode()
    {
        var response = AppResponse.Error("field.name", "invalid");

        response.Messages.Should().ContainSingle()
            .Which.Should().Match<AppMessage>(m => m.Code == "field.name" && m.Description == "invalid");
    }

    [Fact]
    public void Error_WithMessages_ShouldAggregateAll()
    {
        var response = AppResponse.Error(
            new AppMessage("field.a", "bad a"),
            new AppMessage("field.b", "bad b"));

        response.Messages.Should().HaveCount(2);
    }

    [Fact]
    public void AddErrorMessage_ShouldMakeResponseFail()
    {
        var response = AppResponse.Success();

        response.AddErrorMessage("oops");

        response.Failed.Should().BeTrue();
        response.Messages.Should().ContainSingle();
    }
}

public class AppResponseOfTTests
{
    [Fact]
    public void Success_ShouldExposeData()
    {
        var response = AppResponse<string>.Success("payload");

        response.Succeeded.Should().BeTrue();
        response.Data.Should().Be("payload");
    }

    [Fact]
    public void Error_ShouldFailWithoutData()
    {
        var response = AppResponse<string>.Error("field.x", "bad");

        response.Failed.Should().BeTrue();
        response.Data.Should().BeNull();
        response.Messages.Should().ContainSingle();
    }

    [Fact]
    public void AddErrorMessage_ShouldReturnSameResponseForChaining()
    {
        var response = AppResponse<string>.Success("payload");

        var result = response.AddErrorMessage(new AppMessage("field.y", "bad"));

        result.Should().BeSameAs(response);
        response.Failed.Should().BeTrue();
    }
}

public class PagingTests
{
    [Fact]
    public void Defaults_ShouldBeTenPerFirstPage()
    {
        var paging = new Paging();

        paging.PageSize.Should().Be(10);
        paging.PageNumber.Should().Be(0);
    }
}

public class AppResponseExtensionsTests
{
    [Fact]
    public void AddPrefix_ShouldPrefixEveryMessageCode()
    {
        var messages = new[]
        {
            new AppMessage("Name", "name is required"),
            new AppMessage("Amount", "amount is negative"),
        };

        var prefixed = messages.AddPrefix("Expense").ToList();

        prefixed.Should().HaveCount(2);
        prefixed[0].Code.Should().Be("Expense.Name");
        prefixed[1].Code.Should().Be("Expense.Amount");
        prefixed[0].Description.Should().Be("name is required");
    }
}
