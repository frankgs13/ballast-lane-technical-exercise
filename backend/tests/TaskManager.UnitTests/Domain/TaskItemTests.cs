using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.UnitTests.Domain;

public class TaskItemTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 2, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(NowUtc);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_SetsDefaultsAndTrimsValues()
    {
        var task = TaskItem.Create(UserId, "  Write README  ", "  Setup steps  ", Today.AddDays(3), NowUtc);

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(UserId, task.UserId);
        Assert.Equal("Write README", task.Title);
        Assert.Equal("Setup steps", task.Description);
        Assert.Equal(TaskItemStatus.Pending, task.Status);
        Assert.Equal(Today.AddDays(3), task.DueDate);
        Assert.Equal(NowUtc, task.CreatedAtUtc);
        Assert.Equal(NowUtc, task.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutTitle_Throws(string? title)
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => TaskItem.Create(UserId, title, null, Today, NowUtc));

        Assert.True(ex.Errors.ContainsKey("title"));
    }

    [Fact]
    public void Create_WithTitleTooLong_Throws()
    {
        var title = new string('a', TaskItem.TitleMaxLength + 1);

        var ex = Assert.Throws<DomainValidationException>(
            () => TaskItem.Create(UserId, title, null, Today, NowUtc));

        Assert.True(ex.Errors.ContainsKey("title"));
    }

    [Fact]
    public void Create_WithDescriptionTooLong_Throws()
    {
        var description = new string('a', TaskItem.DescriptionMaxLength + 1);

        var ex = Assert.Throws<DomainValidationException>(
            () => TaskItem.Create(UserId, "Title", description, Today, NowUtc));

        Assert.True(ex.Errors.ContainsKey("description"));
    }

    [Fact]
    public void Create_WithWhitespaceDescription_StoresNull()
    {
        var task = TaskItem.Create(UserId, "Title", "   ", Today, NowUtc);

        Assert.Null(task.Description);
    }

    [Fact]
    public void Create_WithoutDueDate_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => TaskItem.Create(UserId, "Title", null, null, NowUtc));

        Assert.True(ex.Errors.ContainsKey("dueDate"));
    }

    [Fact]
    public void Create_WithDueDateInThePast_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => TaskItem.Create(UserId, "Title", null, Today.AddDays(-2), NowUtc));

        Assert.True(ex.Errors.ContainsKey("dueDate"));
    }

    [Fact]
    public void Create_WithDueDateYesterdayInUtc_IsAllowedToTolerateClientTimeZones()
    {
        // A user in UTC-6 at 19:00 local time is already "tomorrow" in UTC.
        var task = TaskItem.Create(UserId, "Title", null, Today.AddDays(-1), NowUtc);

        Assert.Equal(Today.AddDays(-1), task.DueDate);
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => TaskItem.Create(Guid.Empty, "Title", null, Today, NowUtc));
    }

    [Fact]
    public void Create_WithSeveralInvalidFields_ReportsAllErrorsAtOnce()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => TaskItem.Create(UserId, "", new string('a', 5000), null, NowUtc));

        Assert.Equal(3, ex.Errors.Count);
    }

    [Fact]
    public void Update_WithValidData_ChangesValuesAndUpdatedAt()
    {
        var task = TaskItem.Create(UserId, "Title", null, Today, NowUtc);
        var later = NowUtc.AddHours(1);

        task.Update("New title", "New description", TaskItemStatus.InProgress, Today.AddDays(5), later);

        Assert.Equal("New title", task.Title);
        Assert.Equal("New description", task.Description);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(Today.AddDays(5), task.DueDate);
        Assert.Equal(NowUtc, task.CreatedAtUtc);
        Assert.Equal(later, task.UpdatedAtUtc);
    }

    [Fact]
    public void Update_WithoutStatus_Throws()
    {
        var task = TaskItem.Create(UserId, "Title", null, Today, NowUtc);

        var ex = Assert.Throws<DomainValidationException>(
            () => task.Update("Title", null, null, Today, NowUtc));

        Assert.True(ex.Errors.ContainsKey("status"));
    }

    [Fact]
    public void Update_WithUndefinedStatus_Throws()
    {
        var task = TaskItem.Create(UserId, "Title", null, Today, NowUtc);

        var ex = Assert.Throws<DomainValidationException>(
            () => task.Update("Title", null, (TaskItemStatus)99, Today, NowUtc));

        Assert.True(ex.Errors.ContainsKey("status"));
    }

    [Fact]
    public void Update_OverdueTaskKeepingSameDueDate_IsAllowed()
    {
        var task = TaskItem.Create(UserId, "Title", null, Today, NowUtc);
        var tenDaysLater = NowUtc.AddDays(10);

        task.Update("Title", null, TaskItemStatus.Completed, Today, tenDaysLater);

        Assert.Equal(TaskItemStatus.Completed, task.Status);
    }

    [Fact]
    public void Update_MovingDueDateToThePast_Throws()
    {
        var task = TaskItem.Create(UserId, "Title", null, Today.AddDays(5), NowUtc);

        var ex = Assert.Throws<DomainValidationException>(
            () => task.Update("Title", null, TaskItemStatus.Pending, Today.AddDays(-5), NowUtc));

        Assert.True(ex.Errors.ContainsKey("dueDate"));
    }

    [Fact]
    public void Update_WithInvalidData_DoesNotMutateTheEntity()
    {
        var task = TaskItem.Create(UserId, "Title", "Description", Today, NowUtc);

        Assert.Throws<DomainValidationException>(
            () => task.Update("", "Changed", TaskItemStatus.Completed, Today, NowUtc));

        Assert.Equal("Title", task.Title);
        Assert.Equal("Description", task.Description);
        Assert.Equal(TaskItemStatus.Pending, task.Status);
    }

    [Theory]
    [InlineData(TaskItemStatus.Pending, -1, true)]
    [InlineData(TaskItemStatus.InProgress, -1, true)]
    [InlineData(TaskItemStatus.Completed, -1, false)]
    [InlineData(TaskItemStatus.Pending, 0, false)]
    public void IsOverdue_DependsOnStatusAndDueDate(TaskItemStatus status, int dueInDays, bool expected)
    {
        var task = TaskItem.Rehydrate(Guid.NewGuid(), UserId, "Title", null, status,
            Today.AddDays(dueInDays), NowUtc, NowUtc);

        Assert.Equal(expected, task.IsOverdue(Today));
    }
}
