using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;

namespace Agency.Huddle.AnalyzerProbes;

// Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.
// The file is meant to be wrong; do not copy anything from it.

public class WebModel
{
    // probe: S6964
    public int Count { get; set; }

    public string? Name { get; set; }
}

[Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
public class RouteBackslashController : Controller
{
    [HttpGet("a\b")]
    public IActionResult Get() => Ok();

    [HttpGet("/absolute")]
    public IActionResult Absolute() => Ok();

    // probe: S6932
    public IActionResult Raw() { var v = Request.Form["x"]; return Ok(v); }

    // probe: S6962
    public IActionResult Client() { using HttpClient client = new(); return Ok(client.BaseAddress); }

    [HttpPost]
    public IActionResult Post([FromBody] WebModel model) { return Ok(model); }

    [HttpPost("x")]
    public IActionResult Post2([FromBody] WebModel model) { return Ok(model.Count); }
}

// probe: S6934
public class NoRouteController : Controller
{
    [HttpGet("x")]
    public IActionResult Get() => Ok();
}

// probe: S6961
[ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/x")]
public class ApiFromController : Controller
{
    [HttpGet]
    public IActionResult Get() => Ok();
}

[ApiController]
[Microsoft.AspNetCore.Mvc.Route("api/y")]
public class VerblessController : ControllerBase
{
    // probe: S6965
    public IActionResult Verbless() => Ok();

    [HttpGet("z")]
    public WebModel Typed() => new();
}

public class MixedController : Controller
{
    public IActionResult Page() => View();

    [HttpGet("data")]
    public IActionResult Data() => Json(new { x = 1 });

    [HttpPost("save")]
    public IActionResult Save() => Ok();

    [HttpDelete("remove")]
    public IActionResult Remove() => Ok();
}

public class CookieProbes
{
    public void Secure() { /* probe: S2092 */ CookieOptions options = new() { Secure = false }; }

    public void HttpOnly() { /* probe: S3330 */ CookieOptions options = new() { HttpOnly = false }; }

    public void Cors(Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder builder) { /* probe: S5122 */ builder.AllowAnyOrigin(); }

    // probe: S5693
    [DisableRequestSizeLimit]
    public IActionResult Big() => new OkResult();

    public void Csp(HttpResponse response) { /* probe: S7039 */ response.Headers["Content-Security-Policy"] = "default-src *"; }

    public void DevPage(Microsoft.AspNetCore.Builder.IApplicationBuilder app) { /* probe: S4507 */ Microsoft.AspNetCore.Builder.DeveloperExceptionPageExtensions.UseDeveloperExceptionPage(app); }
}

// probe: S4502
[IgnoreAntiforgeryToken]
public class NoCsrfController : Controller
{
    [HttpPost("csrf")]
    public IActionResult Post() => Ok();
}

public class BlazorProbes : ComponentBase
{
    [SupplyParameterFromQuery]
    public WebModel? Query { get; set; }

    // probe: S6798
    [JSInvokable]
    private void FromJs() { }
}

// probe: S6800
[Microsoft.AspNetCore.Components.RouteAttribute("/items/{Id:int}")]
public class RouteTypeMismatch : ComponentBase
{
    [Parameter]
    public string Id { get; set; } = "";
}
