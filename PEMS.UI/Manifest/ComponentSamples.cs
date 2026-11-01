using System.Text.Json.Nodes;

namespace PEMS.UI.Manifest;

/// <summary>
/// One small valid options JSON per component key: shown by the component catalog (#326) and used by tests to prove that the generated
/// schema accepts real input and that the component renders from it. Sample texts are data, not component text.
/// </summary>
public static class ComponentSamples
{
    private static readonly Dictionary<string, string> Json = new(StringComparer.OrdinalIgnoreCase)
    {
        ["button"] = """{"label":{"en":"Save","fa":"ذخیره"},"tone":"primary"}""",
        ["badge"] = """{"label":{"en":"Active","fa":"فعال"},"tone":"success"}""",
        ["skeleton"] = """{"lines":3}""",
        ["empty-state"] = """{"title":{"en":"No records","fa":"رکوردی نیست"},"icon":"bi-inbox"}""",
        ["error-state"] = """{"title":{"en":"Could not load","fa":"بارگذاری نشد"}}""",
        ["loading-state"] = """{"title":{"en":"Loading","fa":"در حال بارگذاری"},"lines":2}""",
        ["field"] = """{"descriptor":{"name":"tag","label":{"en":"Tag","fa":"تگ"}},"value":"PT-0001"}""",
        ["table"] = """{"view":{"id":"items","title":{"en":"Items","fa":"موارد"},"columns":[{"key":"tag","header":{"en":"Tag","fa":"تگ"}}]},"rows":[]}""",
        ["form-section"] = """{"options":{"title":{"en":"General","fa":"عمومی"},"fields":[{"name":"tag","label":{"en":"Tag","fa":"تگ"}}]}}""",
        ["action-bar"] = """{"actions":[{"label":{"en":"Save","fa":"ذخیره"},"type":"submit"},{"label":{"en":"Cancel","fa":"انصراف"},"tone":"neutral","href":"/x"}],"sticky":true}""",
        ["alert"] = """{"title":{"en":"Could not save","fa":"ذخیره نشد"},"tone":"danger","traceId":"00-abc","copyLabel":{"en":"Copy","fa":"کپی"}}""",
        ["modal"] = """{"id":"m1","title":{"en":"Details","fa":"جزئیات"},"closeLabel":{"en":"Close","fa":"بستن"},"body":{"en":"Text","fa":"متن"}}""",
        ["drawer"] = """{"id":"d1","title":{"en":"Filters","fa":"فیلترها"},"closeLabel":{"en":"Close","fa":"بستن"},"side":"end"}""",
        ["confirm-dialog"] = """{"id":"c1","action":"/x/cancel","title":{"en":"Cancel order","fa":"لغو سفارش"},"message":{"en":"Are you sure?","fa":"مطمئن هستید؟"},"confirmLabel":{"en":"Yes","fa":"بله"},"cancelLabel":{"en":"No","fa":"خیر"},"reasonLabel":{"en":"Reason","fa":"دلیل"}}""",
        ["tabs"] = """{"id":"t1","label":{"en":"Sections","fa":"بخش‌ها"},"items":[{"key":"a","label":{"en":"General","fa":"عمومی"},"content":{"en":"First","fa":"اول"}},{"key":"b","label":{"en":"Notes","fa":"یادداشت"}}]}""",
        ["breadcrumb"] = """{"label":{"en":"Path","fa":"مسیر"},"items":[{"label":{"en":"Home","fa":"خانه"},"href":"/"},{"label":{"en":"Orders","fa":"سفارش‌ها"}}]}""",
        ["stepper"] = """{"label":{"en":"Stages","fa":"مراحل"},"steps":[{"label":{"en":"Draft","fa":"پیش‌نویس"},"state":"done"},{"label":{"en":"Review","fa":"بررسی"},"state":"current"},{"label":{"en":"Approved","fa":"تأیید"}}]}""",
        ["menu"] = """{"label":{"en":"Actions","fa":"عملیات"},"items":[{"label":{"en":"Edit","fa":"ویرایش"},"href":"/x/edit"}]}""",
        ["side-panel-nav"] = """{"label":{"en":"Folders","fa":"پوشه‌ها"},"items":[{"label":{"en":"Inbox","fa":"دریافتی"},"href":"/l","count":12,"active":true}]}""",
        ["card"] = """{"cssClass":"shadow-sm"}""",
        ["progress"] = """{"value":40,"tone":"success","height":"8px"}""",
        ["accordion"] = """{"id":"a1"}""",
        ["accordion-item"] = """{"target":"a1-item","parentId":"a1"}""",
        ["list-group"] = """{"flush":true}""",
        ["spinner"] = """{"size":"small","tone":"neutral"}""",
        ["form"] = """{"definition":{"id":"item","title":{"en":"Item","fa":"مورد"},"action":"/items","fields":[{"name":"tag","label":{"en":"Tag","fa":"تگ"}}],"submitLabel":{"en":"Save","fa":"ذخیره"}}}"""
    };

    public static JsonNode? Get(string key) => Json.TryGetValue(key, out var j) ? JsonNode.Parse(j) : null;
}
