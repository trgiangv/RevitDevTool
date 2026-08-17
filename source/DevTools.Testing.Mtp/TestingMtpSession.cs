using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.Testing.Mtp;

public static class TestingMtpSession
{
    public static TestNode CreateErrorNode(string uid, string displayName, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(uid))
            throw new ArgumentException("Error node uid is required.", nameof(uid));
        if (exception is null)
            throw new ArgumentNullException(nameof(exception));

        return new TestNode
        {
            Uid = new TestNodeUid(uid),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? uid : displayName,
            Properties = new PropertyBag(new ErrorTestNodeStateProperty(exception)),
        };
    }
}
