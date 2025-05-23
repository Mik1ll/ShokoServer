using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasTraktAutoLinkingDisabledExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string Name => "Has Trakt Auto Linking Disabled";
    public override string HelpDescription => "This condition passes if any of the anime has Trakt auto-linking disabled";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.HasTraktAutoLinkingDisabled;
    }

    protected bool Equals(HasTraktAutoLinkingDisabledExpression other)
    {
        return base.Equals(other);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((HasTraktAutoLinkingDisabledExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasTraktAutoLinkingDisabledExpression left, HasTraktAutoLinkingDisabledExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTraktAutoLinkingDisabledExpression left, HasTraktAutoLinkingDisabledExpression right)
    {
        return !Equals(left, right);
    }
}
