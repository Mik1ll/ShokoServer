﻿using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.ActionConstraints;
using Shoko.Server.API.Annotations;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v0.Controllers;

[Route("/")]
[ApiVersionNeutral]
[InitFriendly]
[DatabaseBlockedExempt]
public class IndexRedirectController(ISettingsProvider settingsProvider) : Controller
{
    [HttpGet]
    [RedirectConstraint]
    public ActionResult Index()
        => Redirect(settingsProvider.GetSettings().Web.WebUIPublicPath);
}
