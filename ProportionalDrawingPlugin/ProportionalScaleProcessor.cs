using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace ProportionalDrawingPlugin;

internal sealed class ProportionalScaleProcessor : IVideoEffectProcessor
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly ProportionalScaleEffect effect;
    private ID2D1Image? input;

    public ProportionalScaleProcessor(IGraphicsDevicesAndContext devices, ProportionalScaleEffect effect)
    {
        this.devices = devices;
        this.effect = effect;
    }

    public ID2D1Image Output => input ?? throw new InvalidOperationException("入力画像が設定されていません。");
    public void SetInput(ID2D1Image? input) => this.input = input;
    public void ClearInput() => input = null;

    public DrawDescription Update(EffectDescription description)
    {
        var draw = description.DrawDescription;
        if (input is null || description.ScreenSize.Width <= 0 || description.ScreenSize.Height <= 0)
            return draw;

        var bounds = devices.DeviceContext.GetImageLocalBounds(input);
        var sourceWidth = bounds.Right - bounds.Left;
        var sourceHeight = bounds.Bottom - bounds.Top;
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return draw;

        var frame = description.ItemPosition.Frame;
        var length = description.ItemDuration.Frame;
        var fps = description.FPS;
        var overall = effect.Overall.GetValue(frame, length, fps) / 100d;
        var vertical = effect.UseSeparateAxes ? effect.Vertical.GetValue(frame, length, fps) / 100d : 1d;
        var horizontal = effect.UseSeparateAxes ? effect.Horizontal.GetValue(frame, length, fps) / 100d : 1d;
        var fit = effect.Direction == ScaleReferenceDirection.縦幅基準
            ? description.ScreenSize.Height / sourceHeight
            : description.ScreenSize.Width / sourceWidth;

        return draw with
        {
            Zoom = new Vector2(
                (float)(draw.Zoom.X * fit * overall * horizontal),
                (float)(draw.Zoom.Y * fit * overall * vertical)),
        };
    }

    public void Dispose() => input = null;
}
