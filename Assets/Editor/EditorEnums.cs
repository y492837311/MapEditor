namespace MapEditor
{
    public enum ToolType
    {
        Pencil,
        Bucket,
        Eraser,
        Eyedropper,
        Selection
    }

    public enum ErrorType
    {
        None = 0,
        IsolatedPixel = 1,
        ThreeColorIntersection = 2,
        SinglePixelLine = 3,
        ColorConflict = 4
    }

    public enum ExportFormat
    {
        PNG,
        JPG,
        OriginalConfig
    }
}