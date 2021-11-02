
namespace Okr_Lambda.Common
{
    public enum NotificationType
    {
        LoginReminderForUser = 21,

    }
    public enum MessageTypeForNotifications
    {
        NotificationsMessages = 1
    }

    public enum TemplateCodes
    {
        LR = 1,
        LRM = 2,
        NCS = 3,
        WRU = 4,
        WRM = 5,
        GD = 6,
        TNP = 7,
        TENP = 8,
        FDNP = 9,
        CPS = 10,
        DIM = 11,
        LDS = 12,
        DOS = 13,
        PC =14
    }

    public enum KrStatus
    {
        Pending = 1,
        Accepted,
        Declined
    }

    public enum GoalStatus
    {
        Draft = 1,
        Public,
        Archive
    }
}
