using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Player.Video;

namespace ProportionalDrawingPlugin;

internal sealed class ProportionalPositionProcessor : IVideoEffectProcessor
{
    private readonly ProportionalPositionEffect effect;
    private ID2D1Image? input;

    public ProportionalPositionProcessor(ProportionalPositionEffect effect) => this.effect = effect;
    public ID2D1Image Output => input ?? throw new InvalidOperationException("入力画像が設定されていません。");
    public void SetInput(ID2D1Image? input) => this.input = input;
    public void ClearInput() => input = null;

    public DrawDescription Update(EffectDescription description)
    {
        var screen = description.ScreenSize;
        var frame = description.ItemPosition.Frame;
        var length = description.ItemDuration.Frame;
        var fps = description.FPS;
        var x = effect.X.GetValue(frame, length, fps) / 100d;
        var y = effect.Y.GetValue(frame, length, fps) / 100d;
        var z = effect.Z.GetValue(frame, length, fps);

        return description.DrawDescription with
        {
            Draw = new Vector3(
                description.DrawDescription.Draw.X + (float)(screen.Width * x / 2d),
                description.DrawDescription.Draw.Y + (float)(screen.Height * y / 2d),
                description.DrawDescription.Draw.Z + (float)z),
        };
    }

    public void Dispose() => input = null;
}
