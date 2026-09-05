using Microsoft.AspNetCore.Mvc;
using PEMS.AI.Interfaces;

namespace PEMS.Web.Controllers;

public class AIController : Controller
{
    private readonly IOllamaService _ollamaService;

    public AIController(IOllamaService ollamaService)
    {
        _ollamaService = ollamaService;
    }


    public async Task<IActionResult> Test()
    {
        var result = await _ollamaService.GenerateAsync(
            "سلام. خودت را معرفی کن و بگو چه کاری انجام می دهی."
        );


        return Content(result);
    }
}