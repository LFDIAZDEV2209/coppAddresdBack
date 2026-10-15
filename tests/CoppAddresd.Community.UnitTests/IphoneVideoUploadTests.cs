using CoppAddresd.Community.Storage;
namespace CoppAddresd.Community.UnitTests;
public sealed class IphoneVideoUploadTests
{
    [Fact]
    public void MovRecording_IsAcceptedAsVideoAttachment()
    {
        Assert.True(PostStorageEndpoints.IsAllowedImageContentType("video/quicktime"));
        Assert.True(PostStorageEndpoints.IsAllowedImageExtension(".MOV"));
        Assert.True(PostStorageEndpoints.IsValidPostImageKey("community/posts/iphone-recording.mov"));
        Assert.False(PostStorageEndpoints.IsAllowedImageExtension(".exe"));
    }
}
