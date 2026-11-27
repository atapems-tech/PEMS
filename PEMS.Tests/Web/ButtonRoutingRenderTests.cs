using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Models;

namespace PEMS.Tests.Web;

/// <summary>SCRUM-53: a <c>&lt;pems-button as="a" asp-controller asp-action&gt;</c> is written as an anchor with the button classes and none of the routing attributes left over. The test host registers no routes, so the href value itself (made by the built-in AnchorTagHelper this helper derives from) is not asserted here.</summary>
public class ButtonRoutingRenderTests
{
}
