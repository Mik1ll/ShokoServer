﻿using Nancy.Rest.Annotations.Attributes;
using Nancy.Rest.Annotations.Enums;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/api/Image")]
    public interface IShokoServerImage
    {
        [Rest("{imageid}/{imageType}/{thumnbnailOnly?}", Verbs.Get)]
        object GetImage(int imageid, int imageType, bool? thumnbnailOnly);

        [Rest("Blank", Verbs.Get)]
        object BlankImage();

        [Rest("Support/{name}/{ratio}", Verbs.Get)]
        object GetSupportImage(string name, float? ratio);

        [Rest("Thumb/{imageId}/{imageType}/{ratio}", Verbs.Get)]
        object GetThumb(int imageId, int imageType, float ratio);

        [Rest("Path/{imageId}/{imageType}/{thumnbnailOnly?}", Verbs.Get)]
        string GetImagePath(int imageId, int imageType, bool? thumnbnailOnly);

    }
}
