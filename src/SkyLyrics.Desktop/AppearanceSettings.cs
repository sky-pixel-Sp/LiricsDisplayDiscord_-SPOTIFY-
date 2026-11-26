using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;

namespace SkyLyrics.Desktop;

public sealed record AppearanceSettings(
    string AnimationId,
    string EffectId,
    string BackgroundId)
{
    public static AppearanceSettings Default { get; } =
        new("soft-fade", "mint-halo", "midnight-aurora");
}

public static class AppearanceSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SkyLyrics",
        "appearance.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppearanceSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return AppearanceSettings.Default;

            var loaded = JsonSerializer.Deserialize<AppearanceSettings>(
                File.ReadAllText(SettingsPath), JsonOptions);

            if (loaded is null)
                return AppearanceSettings.Default;

            return new AppearanceSettings(
                AppearanceCatalog.GetAnimation(loaded.AnimationId).Id,
                AppearanceCatalog.GetEffect(loaded.EffectId).Id,
                AppearanceCatalog.GetBackground(loaded.BackgroundId).Id);
        }
        catch
        {
            return AppearanceSettings.Default;
        }
    }

    public static void Save(AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }
}

public sealed record AnimationPreset(
    string Id,
    string Name,
    int DurationMs,
    double FromX,
    double FromY,
    double ScaleFrom,
    double RotationFrom,
    string Easing);

public sealed record EffectPreset(
    string Id,
    string Name,
    string Color,
    double BlurRadius,
    double ShadowDepth,
    double Opacity,
    double Direction);

public sealed record BackgroundPreset(
    string Id,
    string Name,
    string Start,
    string Middle,
    string End,
    double Angle);

public static class AppearanceCatalog
{
    public static IReadOnlyList<AnimationPreset> Animations { get; } = new[]
    {
        new AnimationPreset("soft-fade", "01 · Soft Fade", 280, 0, 0, 1, 0, "cubic"),
        new AnimationPreset("slide-up", "02 · Slide Up", 300, 0, 24, 1, 0, "cubic"),
        new AnimationPreset("slide-down", "03 · Slide Down", 300, 0, -24, 1, 0, "cubic"),
        new AnimationPreset("slide-left", "04 · Slide Left", 300, 28, 0, 1, 0, "cubic"),
        new AnimationPreset("slide-right", "05 · Slide Right", 300, -28, 0, 1, 0, "cubic"),
        new AnimationPreset("zoom-in", "06 · Zoom In", 320, 0, 0, .76, 0, "back"),
        new AnimationPreset("zoom-out", "07 · Zoom Out", 320, 0, 0, 1.22, 0, "cubic"),
        new AnimationPreset("pop-bounce", "08 · Pop Bounce", 430, 0, 9, .72, 0, "bounce"),
        new AnimationPreset("spring-pop", "09 · Spring Pop", 520, 0, 14, .64, 0, "elastic"),
        new AnimationPreset("bounce-rise", "10 · Bounce Rise", 440, 0, 30, 1, 0, "bounce"),
        new AnimationPreset("flip-clockwise", "11 · Flip Clockwise", 360, 0, 0, .92, 82, "cubic"),
        new AnimationPreset("flip-counterclockwise", "12 · Flip Counterclockwise", 360, 0, 0, .92, -82, "cubic"),
        new AnimationPreset("rotate-in", "13 · Rotate In", 340, 0, 0, .9, 14, "cubic"),
        new AnimationPreset("rotate-out", "14 · Rotate Out", 340, 0, 0, .9, -14, "cubic"),
        new AnimationPreset("swing", "15 · Swing", 500, 0, 0, 1, -18, "elastic"),
        new AnimationPreset("drift-up", "16 · Drift Up", 520, 0, 16, 1, 0, "sine"),
        new AnimationPreset("drift-down", "17 · Drift Down", 520, 0, -16, 1, 0, "sine"),
        new AnimationPreset("diagonal-forward", "18 · Diagonal Forward", 340, 24, 18, 1, 0, "cubic"),
        new AnimationPreset("diagonal-reverse", "19 · Diagonal Reverse", 340, -24, -18, 1, 0, "cubic"),
        new AnimationPreset("scale-pulse", "20 · Scale Pulse", 380, 0, 0, .88, 0, "back"),
        new AnimationPreset("micro-zoom", "21 · Micro Zoom", 220, 0, 0, .94, 0, "quadratic"),
        new AnimationPreset("float-in", "22 · Float In", 620, 0, 34, 1, 0, "sine"),
        new AnimationPreset("elastic-slide", "23 · Elastic Slide", 560, 34, 0, 1, 0, "elastic"),
        new AnimationPreset("tilt-left", "24 · Tilt Left", 340, 0, 8, 1, 9, "cubic"),
        new AnimationPreset("tilt-right", "25 · Tilt Right", 340, 0, 8, 1, -9, "cubic"),
        new AnimationPreset("quick-cut", "26 · Quick Cut", 120, 0, 0, 1, 0, "linear"),
        new AnimationPreset("slow-reveal", "27 · Slow Reveal", 720, 0, 5, 1, 0, "sine"),
        new AnimationPreset("sharp-snap", "28 · Sharp Snap", 180, 0, 3, .97, 0, "exponential"),
        new AnimationPreset("cinematic-rise", "29 · Cinematic Rise", 680, 0, 28, 1.03, 0, "circle"),
        new AnimationPreset("minimal-shift", "30 · Minimal Shift", 240, 0, 5, 1, 0, "quadratic")
    };

    public static IReadOnlyList<EffectPreset> Effects { get; } = new[]
    {
        new EffectPreset("no-effect", "01 · No Effect", "#000000", 0, 0, 0, 0),
        new EffectPreset("mint-halo", "02 · Mint Halo", "#67D7B0", 18, 0, .72, 270),
        new EffectPreset("soft-white", "03 · Soft White Glow", "#F3FFF8", 16, 0, .58, 270),
        new EffectPreset("ice-blue", "04 · Ice Blue Glow", "#79D8FF", 20, 0, .78, 270),
        new EffectPreset("violet-halo", "05 · Violet Halo", "#AB8CFF", 20, 0, .72, 270),
        new EffectPreset("rose-halo", "06 · Rose Halo", "#FF8EBA", 18, 0, .68, 270),
        new EffectPreset("amber-halo", "07 · Amber Halo", "#FFC56E", 18, 0, .7, 270),
        new EffectPreset("emerald-edge", "08 · Emerald Edge", "#36D99B", 7, 2, .82, 270),
        new EffectPreset("ocean-edge", "09 · Ocean Edge", "#34BDEB", 8, 2, .82, 270),
        new EffectPreset("deep-shadow", "10 · Deep Shadow", "#020806", 13, 5, .64, 270),
        new EffectPreset("soft-shadow", "11 · Soft Shadow", "#000000", 9, 3, .3, 270),
        new EffectPreset("floating-shadow", "12 · Floating Shadow", "#000000", 15, 8, .42, 270),
        new EffectPreset("crisp-shadow", "13 · Crisp Shadow", "#000000", 2, 2, .48, 270),
        new EffectPreset("golden-halo", "14 · Golden Halo", "#FFE08C", 22, 0, .66, 270),
        new EffectPreset("cobalt-glow", "15 · Cobalt Glow", "#4D86FF", 17, 0, .76, 270),
        new EffectPreset("coral-glow", "16 · Coral Glow", "#FF927E", 17, 0, .74, 270),
        new EffectPreset("arctic-glow", "17 · Arctic Glow", "#BCEBFF", 25, 0, .54, 270),
        new EffectPreset("forest-glow", "18 · Forest Glow", "#8AD49A", 15, 0, .7, 270),
        new EffectPreset("plum-glow", "19 · Plum Glow", "#C08BDB", 19, 0, .72, 270),
        new EffectPreset("silver-halo", "20 · Silver Halo", "#D7E4E0", 12, 0, .62, 270),
        new EffectPreset("neon-mint", "21 · Neon Mint", "#00FFB3", 28, 0, .86, 270),
        new EffectPreset("neon-violet", "22 · Neon Violet", "#A64DFF", 27, 0, .82, 270),
        new EffectPreset("neon-blue", "23 · Neon Blue", "#3D9BFF", 27, 0, .82, 270),
        new EffectPreset("sunset-edge", "24 · Sunset Edge", "#FF9F67", 11, 2, .78, 270),
        new EffectPreset("ruby-shadow", "25 · Ruby Shadow", "#C83E61", 14, 4, .68, 270),
        new EffectPreset("aqua-halo", "26 · Aqua Halo", "#53E4D0", 21, 0, .76, 270),
        new EffectPreset("lavender-halo", "27 · Lavender Halo", "#C5B5FF", 18, 0, .68, 270),
        new EffectPreset("graphite-shadow", "28 · Graphite Shadow", "#18201C", 7, 4, .52, 270),
        new EffectPreset("cinematic-depth", "29 · Cinematic Depth", "#000000", 18, 7, .5, 270),
        new EffectPreset("warm-diffuse", "30 · Warm Diffuse", "#FFD2A4", 24, 1, .5, 270)
    };

    public static IReadOnlyList<BackgroundPreset> Backgrounds { get; } = new[]
    {
        new BackgroundPreset("midnight-aurora", "01 · Midnight Aurora", "#0B1522", "#17313B", "#173A35", 35),
        new BackgroundPreset("deep-ocean", "02 · Deep Ocean", "#071D2B", "#0C3B50", "#12606A", 135),
        new BackgroundPreset("indigo-night", "03 · Indigo Night", "#10132B", "#252C58", "#3A326E", 45),
        new BackgroundPreset("violet-dusk", "04 · Violet Dusk", "#201631", "#4B2D5C", "#835078", 125),
        new BackgroundPreset("crimson-ember", "05 · Crimson Ember", "#241219", "#5A2630", "#994B3C", 35),
        new BackgroundPreset("rose-quartz", "06 · Rose Quartz", "#321E2C", "#73485F", "#B97886", 140),
        new BackgroundPreset("solar-flare", "07 · Solar Flare", "#312014", "#76502B", "#B77B3D", 45),
        new BackgroundPreset("emerald-forest", "08 · Emerald Forest", "#0B211C", "#17483A", "#32765B", 135),
        new BackgroundPreset("mint-glass", "09 · Mint Glass", "#112C2B", "#246257", "#4A927C", 35),
        new BackgroundPreset("arctic-frost", "10 · Arctic Frost", "#142633", "#31566A", "#66899A", 145),
        new BackgroundPreset("blue-hour", "11 · Blue Hour", "#111C32", "#244B6B", "#477C99", 45),
        new BackgroundPreset("sapphire-depth", "12 · Sapphire Depth", "#081A3A", "#173D79", "#326BA0", 135),
        new BackgroundPreset("soft-lavender", "13 · Soft Lavender", "#27243B", "#51466D", "#8175A2", 35),
        new BackgroundPreset("smoke-glass", "14 · Smoke Glass", "#161D22", "#303B43", "#58656B", 145),
        new BackgroundPreset("graphite", "15 · Graphite", "#111516", "#293130", "#424B47", 45),
        new BackgroundPreset("charcoal", "16 · Charcoal", "#101211", "#242A27", "#343D37", 135),
        new BackgroundPreset("warm-sand", "17 · Warm Sand", "#33291D", "#6A573D", "#9F815B", 35),
        new BackgroundPreset("copper-dusk", "18 · Copper Dusk", "#2E1C18", "#68402D", "#A86D43", 145),
        new BackgroundPreset("coral-bloom", "19 · Coral Bloom", "#321D25", "#78414B", "#B96E6A", 45),
        new BackgroundPreset("berry-night", "20 · Berry Night", "#21162D", "#51264E", "#813E69", 135),
        new BackgroundPreset("teal-current", "21 · Teal Current", "#09252A", "#15515A", "#27827A", 35),
        new BackgroundPreset("jade-mist", "22 · Jade Mist", "#152922", "#315C42", "#6F8D62", 145),
        new BackgroundPreset("storm-cloud", "23 · Storm Cloud", "#1C2630", "#3D4B5C", "#647184", 45),
        new BackgroundPreset("steel-blue", "24 · Steel Blue", "#172532", "#36536A", "#57788B", 135),
        new BackgroundPreset("plum-velvet", "25 · Plum Velvet", "#221A2C", "#49334E", "#73506E", 35),
        new BackgroundPreset("night-sky", "26 · Night Sky", "#0B1020", "#182B4C", "#3A4770", 145),
        new BackgroundPreset("autumn-leaf", "27 · Autumn Leaf", "#2A1D18", "#67402D", "#9C6239", 45),
        new BackgroundPreset("glacier", "28 · Glacier", "#12242C", "#2A5660", "#5D8B8D", 135),
        new BackgroundPreset("soft-cream", "29 · Soft Cream", "#34312A", "#68604B", "#A69A79", 35),
        new BackgroundPreset("monochrome", "30 · Monochrome", "#151718", "#33383A", "#575E60", 145)
    };

    public static AnimationPreset GetAnimation(string? id) =>
        Animations.FirstOrDefault(item => item.Id == id) ?? Animations[0];

    public static EffectPreset GetEffect(string? id) =>
        Effects.FirstOrDefault(item => item.Id == id) ?? Effects[0];

    public static BackgroundPreset GetBackground(string? id) =>
        Backgrounds.FirstOrDefault(item => item.Id == id) ?? Backgrounds[0];

    public static Brush CreateBackgroundBrush(string? id)
    {
        var preset = GetBackground(id);
        var radians = preset.Angle * Math.PI / 180.0;
        var dx = Math.Cos(radians) * 0.5;
        var dy = Math.Sin(radians) * 0.5;
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5 - dx, 0.5 - dy),
            EndPoint = new Point(0.5 + dx, 0.5 + dy)
        };
        brush.GradientStops.Add(new GradientStop(ParseColor(preset.Start), 0));
        brush.GradientStops.Add(new GradientStop(ParseColor(preset.Middle), 0.54));
        brush.GradientStops.Add(new GradientStop(ParseColor(preset.End), 1));
        brush.Freeze();
        return brush;
    }

    public static Effect? CreateTextEffect(string? id)
    {
        var preset = GetEffect(id);
        if (preset.Id == "no-effect")
            return null;

        return new DropShadowEffect
        {
            Color = ParseColor(preset.Color),
            BlurRadius = preset.BlurRadius,
            ShadowDepth = preset.ShadowDepth,
            Opacity = preset.Opacity,
            Direction = preset.Direction,
            RenderingBias = RenderingBias.Performance
        };
    }

    public static IEasingFunction CreateEasing(string easing) =>
        easing switch
        {
            "quadratic" => new QuadraticEase { EasingMode = EasingMode.EaseOut },
            "bounce" => new BounceEase { Bounces = 2, Bounciness = 2.5, EasingMode = EasingMode.EaseOut },
            "elastic" => new ElasticEase { Oscillations = 2, Springiness = 4, EasingMode = EasingMode.EaseOut },
            "back" => new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut },
            "sine" => new SineEase { EasingMode = EasingMode.EaseOut },
            "circle" => new CircleEase { EasingMode = EasingMode.EaseOut },
            "exponential" => new ExponentialEase { EasingMode = EasingMode.EaseOut },
            "linear" => new LinearEasing(),
            _ => new CubicEase { EasingMode = EasingMode.EaseOut }
        };

    public static void AnimateActiveLine(
        TextBlockTransforms transforms,
        string? animationId)
    {
        var preset = GetAnimation(animationId);
        var duration = new Duration(TimeSpan.FromMilliseconds(preset.DurationMs));
        var easing = CreateEasing(preset.Easing);

        transforms.Move.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(preset.FromX, 0, duration) { EasingFunction = easing });
        transforms.Move.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(preset.FromY, 0, duration) { EasingFunction = easing });
        transforms.Scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(preset.ScaleFrom, 1, duration) { EasingFunction = easing });
        transforms.Scale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(preset.ScaleFrom, 1, duration) { EasingFunction = easing });
        transforms.Rotate.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(preset.RotationFrom, 0, duration) { EasingFunction = easing });
    }

    public static TextBlockTransforms EnsureTransforms(TextBlock block)
    {
        if (block.RenderTransform is TransformGroup current)
        {
            var move = current.Children.OfType<TranslateTransform>().FirstOrDefault();
            var scale = current.Children.OfType<ScaleTransform>().FirstOrDefault();
            var rotate = current.Children.OfType<RotateTransform>().FirstOrDefault();
            if (move is not null && scale is not null && rotate is not null)
                return new TextBlockTransforms(move, scale, rotate);
        }

        var initialMove = block.RenderTransform as TranslateTransform ?? new TranslateTransform(0, 5);
        var result = new TransformGroup();
        result.Children.Add(initialMove);
        var scaleTransform = new ScaleTransform(1, 1);
        var rotateTransform = new RotateTransform(0);
        result.Children.Add(scaleTransform);
        result.Children.Add(rotateTransform);
        block.RenderTransform = result;
        block.RenderTransformOrigin = new Point(0.5, 0.5);
        return new TextBlockTransforms(initialMove, scaleTransform, rotateTransform);
    }

    public static Color ParseColor(string value) =>
        (Color)ColorConverter.ConvertFromString(value);

}

public sealed record TextBlockTransforms(
    TranslateTransform Move,
    ScaleTransform Scale,
    RotateTransform Rotate);

internal sealed class LinearEasing : IEasingFunction
{
    public double Ease(double normalizedTime) => normalizedTime;
}
