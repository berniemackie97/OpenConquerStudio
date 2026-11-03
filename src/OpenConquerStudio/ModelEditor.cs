using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using OpenConquer.Content.Mesh;

namespace OpenConquerStudio;

/// <summary>Edits model playback metadata and inspects the native matrix channels without inventing a skeleton hierarchy.</summary>
internal sealed record ModelEdit(string Name, int Clip, int Milliseconds, bool Repeat);

internal sealed class ModelEditor : StackPanel
{
    private readonly TextBox _name = new() { Name = "ModelName" };
    private readonly ComboBox _clips = new() { Name = "ModelClip" };
    private readonly NumericUpDown _milliseconds = new() { Minimum = 1, Maximum = 60000, Value = 66, Name = "ModelFrameMilliseconds" };
    private readonly CheckBox _repeat = new() { Content = "Repeat", IsChecked = true, Name = "ModelRepeat" };
    private readonly CheckBox _bones = new() { Content = "Show bone channel origins", Name = "ShowModelBones" };
    private readonly NumericUpDown _rotation = new() { Minimum = -180, Maximum = 180, Value = 0, Name = "ModelRotation" };
    private readonly ComboBox _tracks = new() { Name = "ModelTrack" };
    private readonly NumericUpDown _bone = new() { Minimum = 0, Maximum = 0, Value = 0, Name = "ModelBone" };
    private readonly TextBlock _details = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _matrix = new() { FontFamily = new Avalonia.Media.FontFamily("monospace"), FontSize = 11, Name = "ModelBoneMatrix" };
    private C3ModelTrack[] _allTracks = [];
    private bool _loading;
    private int _frame;
    private ModelEdit? _loaded;
    public event Action? PendingChanged;
    public int ActiveClip => _loaded?.Clip ?? -1;
    public bool HasPending
    {
        get
        {
            if (_loaded is null)
            {
                return false;
            }
            try
            {
                return ReadChanges() != _loaded;
            }
            catch (InvalidDataException) { return true; }
        }
    }
    public ModelEdit ReadChanges() => new(_name.Text ?? "", _loaded?.Clip ?? -1,
        _loaded?.Clip >= 0 ? EditorInput.Integer(_milliseconds, "milliseconds per frame") : 66, _loaded?.Clip < 0 || _repeat.IsChecked == true);
    public void RestoreClipSelection()
    {
        _loading = true;
        try
        {
            _clips.SelectedIndex = (_loaded?.Clip ?? -1) + 1;
        }
        finally { _loading = false; }
    }
    public void AcceptPending() => _loaded = ReadChanges();

    public ModelEditor(Func<string, int, int, bool, Task> apply, Func<int, Task> selectClip, Func<Task> addMotion, Action<bool, float> view, Func<Task> previewMap)
    {
        Spacing = 8;
        IsVisible = false;
        Button save = new()
        {
            Content = "Apply model edits",
            Name = "ApplyModelEdits"
        };
        save.Click += async (_, _) => await apply(_name.Text ?? "", _clips.SelectedIndex - 1,
            (int)(_milliseconds.Value ?? 66), _repeat.IsChecked == true);
        Button motion = new()
        {
            Content = "Add C3 motion",
            Name = "AddModelMotion"
        };
        motion.Click += async (_, _) => await addMotion();
        _name.TextChanged += (_, _) => Changed();
        _repeat.IsCheckedChanged += (_, _) => Changed();
        _milliseconds.PropertyChanged += (_, e) => { if (e.Property == NumericUpDown.TextProperty) { Changed(); } };
        Button map = new()
        {
            Content = "Preview on last map",
            Name = "PreviewModelOnMap"
        };
        map.Click += async (_, _) => await previewMap();
        Children.Add(map);
        _clips.SelectionChanged += async (_, _) => { if (!_loading) { await selectClip(_clips.SelectedIndex - 1); } };
        _tracks.SelectionChanged += (_, _) => DescribeTrack();
        _bone.ValueChanged += (_, _) => UpdateFrame(_frame);
        _bones.IsCheckedChanged += (_, _) => view(_bones.IsChecked == true, (float)(_rotation.Value ?? 0));
        _rotation.ValueChanged += (_, _) => view(_bones.IsChecked == true, (float)(_rotation.Value ?? 0));
        foreach (Control control in new Control[]
        {
            new TextBlock { Text = "Model" }, _name, _details,
            new TextBlock { Text = "Animation clip" }, _clips,
            new TextBlock { Text = "Milliseconds per frame" }, _milliseconds, _repeat, save, motion,
            _bones, new TextBlock { Text = "View rotation (degrees)" }, _rotation,
            new TextBlock { Text = "Submesh / matrix track" }, _tracks,
            new TextBlock { Text = "Bone channel index" }, _bone, _matrix,
        })
        {
            Children.Add(control);
        }
    }

    public void Load(string name, C3ModelPreview preview)
    {
        _loading = true;
        try
        {
            _frame = 0;
            _name.Text = name;
            _clips.ItemsSource = preview.Model.Definition.Clips.Select(c => c.Name).Prepend("Embedded pose").ToArray();
            _clips.SelectedIndex = preview.ClipIndex + 1;
            _milliseconds.Value = preview.Clip?.FrameMilliseconds ?? 66;
            _milliseconds.IsEnabled = preview.Clip is not null;
            _repeat.IsChecked = preview.Clip?.Repeat ?? true;
            _repeat.IsEnabled = preview.Clip is not null;
            _allTracks = [.. preview.Parts.SelectMany(p => p.Tracks)];
            _tracks.ItemsSource = _allTracks.Select((t, i) => $"{i}: {t.Mesh.Name}{(t.IsAttachment ? " (attachment)" : "")}").ToArray();
            _tracks.SelectedIndex = _allTracks.Length == 0 ? -1 : 0;
            _details.Text = $"{preview.Model.Definition.Parts.Length} part(s), {_allTracks.Length} tracks, {preview.FrameCount} frames\n" +
                $"{preview.Model.Definition.Sources.Length} preserved source files · {preview.Model.PayloadBytes:N0} bytes\n" +
                "Source files preserved. Material effects and full equipment variants remain incomplete.";
            if (preview.Model.Definition.CharacterSource is { } source)
            {
                _details.Text += $"\nBody catalog ID: {source.BodyModelId} · Headwear: {source.ArmetModelId}";
            }
            _loaded = new ModelEdit(name, preview.ClipIndex, preview.Clip?.FrameMilliseconds ?? 66, preview.Clip?.Repeat ?? true);
            DescribeTrack();
        }
        finally { _loading = false; }
    }

    private void Changed()
    {
        if (!_loading)
        {
            PendingChanged?.Invoke();
        }
    }

    private void DescribeTrack()
    {
        C3ModelTrack? track = _tracks.SelectedIndex < 0 || _tracks.SelectedIndex >= _allTracks.Length ? null : _allTracks[_tracks.SelectedIndex];
        _bone.IsEnabled = track?.Motion?.BoneCount > 0;
        _bone.Maximum = Math.Max(0, (track?.Motion?.BoneCount ?? 0) - 1L);
        _bone.Value = 0;
        UpdateFrame(_frame);
    }

    public void UpdateFrame(int frame)
    {
        _frame = frame;
        if (_tracks.SelectedIndex < 0 || _tracks.SelectedIndex >= _allTracks.Length)
        {
            _matrix.Text = "";
            return;
        }
        C3ModelTrack track = _allTracks[_tracks.SelectedIndex];
        if (track.Motion is null || track.Motion.BoneCount == 0)
        {
            _matrix.Text = "No matrix channels.";
            return;
        }
        if (!C3MotionSampler.TrySampleBone(track.Motion, (uint)(_bone.Value ?? 0), frame, out Matrix4x4 matrix))
        {
            _matrix.Text = "No sample at this frame.";
            return;
        }
        _matrix.Text = string.Create(CultureInfo.InvariantCulture,
            $"Frame {frame} · {track.Motion.BoneCount} channels\n" +
            $"{matrix.M11,9:0.0000} {matrix.M12,9:0.0000} {matrix.M13,9:0.0000} {matrix.M14,9:0.0000}\n" +
            $"{matrix.M21,9:0.0000} {matrix.M22,9:0.0000} {matrix.M23,9:0.0000} {matrix.M24,9:0.0000}\n" +
            $"{matrix.M31,9:0.0000} {matrix.M32,9:0.0000} {matrix.M33,9:0.0000} {matrix.M34,9:0.0000}\n" +
            $"{matrix.M41,9:0.0000} {matrix.M42,9:0.0000} {matrix.M43,9:0.0000} {matrix.M44,9:0.0000}");
    }
}
