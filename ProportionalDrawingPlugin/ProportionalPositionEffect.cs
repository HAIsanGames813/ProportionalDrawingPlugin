using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace ProportionalDrawingPlugin;

[VideoEffect(nameof(Texts.Proportional_position), [VideoEffectCategories.Drawing], [], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class ProportionalPositionEffect : VideoEffectBase
{
    private const double ParameterLimit = 1_000_000_000d;

    public override string Label => $"{Texts.Proportional_position} [{X.GetValue(0, 1, 30):0.#}%, {Y.GetValue(0, 1, 30):0.#}%, {Z.GetValue(0, 1, 30):0.#}%]";

    [Display(GroupName = nameof(Texts.Drawing_position_tab), Name = nameof(Texts.X_position), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation X { get; } = new(0, -ParameterLimit, ParameterLimit);

    [Display(GroupName = nameof(Texts.Drawing_position_tab), Name = nameof(Texts.Y_position), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation Y { get; } = new(0, -ParameterLimit, ParameterLimit);

    [Display(GroupName = nameof(Texts.Drawing_position_tab), Name = nameof(Texts.Z_position), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation Z { get; } = new(0, -ParameterLimit, ParameterLimit);

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        => new ProportionalPositionProcessor(this);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Z];
}
