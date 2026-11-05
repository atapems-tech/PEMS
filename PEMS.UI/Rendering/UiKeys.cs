namespace PEMS.UI.Rendering;

/// <summary>
/// The component keys, the one vocabulary shared by views (through the <c>pems-*</c> tag helpers), descriptors and the dynamic page host (ADR-0013).
/// Code that needs a component by key uses these constants, never a string literal. <see cref="PemsComponentRegistry"/> maps each key to its type.
/// </summary>
public static class UiKeys
{
    public const string Button = "button";
    public const string Badge = "badge";
    public const string Skeleton = "skeleton";
    public const string EmptyState = "empty-state";
    public const string ErrorState = "error-state";
    public const string LoadingState = "loading-state";
    public const string Field = "field";
    public const string Table = "table";
    public const string Form = "form";
    public const string FormSection = "form-section";
    public const string ActionBar = "action-bar";
    public const string Alert = "alert";
    public const string Modal = "modal";
    public const string Drawer = "drawer";
    public const string ConfirmDialog = "confirm-dialog";
    public const string Tabs = "tabs";
    public const string Breadcrumb = "breadcrumb";
    public const string Stepper = "stepper";
    public const string Menu = "menu";
    public const string SidePanelNav = "side-panel-nav";
    public const string Card = "card";
    public const string Progress = "progress";
    public const string Accordion = "accordion";
    public const string AccordionItem = "accordion-item";
    public const string ListGroup = "list-group";
    public const string Spinner = "spinner";
    public const string InfoGrid = "info-grid";
    public const string InfoItem = "info-item";
    public const string KpiTile = "kpi-tile";
}
