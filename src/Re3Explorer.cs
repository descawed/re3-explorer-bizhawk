using System;
using System.Drawing;
using System.Windows.Forms;

using BizHawk.Client.Common;
using BizHawk.Client.EmuHawk;
using BizHawk.Emulation.Common;

namespace Re3Explorer;

[ExternalTool("RE3 Explorer")]
// currently only SLPM-87224 supported
[ExternalToolApplicability.SingleRom(VSystemID.Raw.PSX, GameVersionSlpm87224.Hash)]
public sealed class Re3Explorer: ToolFormBase, IExternalToolForm {
    public ApiContainer? MaybeApi { get; set; }

    private ApiContainer Api => MaybeApi!;

    private readonly GameVersions _gameVersions = new();

    private GameVersion? _version;

    private readonly Label _roomLabel;
    private readonly Label _rngValueLabel;
    private readonly Label _scriptRngValueLabel;
    private readonly Label _scriptRngOffsetIndexLabel;
    private readonly Label _scriptRngOffsetLabel;

    protected override string WindowTitleStatic => "RE3 Explorer";

    public Re3Explorer() {
        Shown += OnShow;
        
        ClientSize = new Size(480, 320);
        SuspendLayout();
        BackColor = Color.LightGray;

        var font = new Font(FontFamily.GenericSansSerif, 13);

        var root = new FlowLayoutPanel {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
        };

        _roomLabel = new Label { AutoSize = true, Font = font, Text = "Room: N/A" };
        
        var table = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        
        // header
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "Value" }, 1, 0);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "Frame" }, 2, 0);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "Room" }, 3, 0);
        table.Controls.Add(new Label { AutoSize = true, Font = font, Text = "Total" }, 4, 0);
        
        // RNG
        table.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = font, Text = "RNG" }, 0, 1);
        _rngValueLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_rngValueLabel, 1, 1);
        
        // script RNG
        table.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = font, Text = "Script RNG" }, 0, 2);
        _scriptRngValueLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_scriptRngValueLabel, 1, 2);
        
        table.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = font, Text = "Script RNG offset index" }, 0, 3);
        _scriptRngOffsetIndexLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_scriptRngOffsetIndexLabel, 1, 3);
        
        table.Controls.Add(new Label { Anchor = AnchorStyles.Right, AutoSize = true, Font = font, Text = "Script RNG offset" }, 0, 4);
        _scriptRngOffsetLabel = new Label { AutoSize = true, Font = font, Text = "" };
        table.Controls.Add(_scriptRngOffsetLabel, 1, 4);
        
        root.Controls.Add(_roomLabel);
        root.Controls.Add(table);
        
        Controls.Add(root);
        
        ResumeLayout(false);
        PerformLayout();
    }

    private void OnShow(object sender, EventArgs e) {
        var parentPosition = Owner.PointToScreen(Point.Empty);
        Top = parentPosition.Y;
        Left = parentPosition.X + Owner.Width;
    }

    public override void Restart() {
        // TODO: reset stats and register event listeners
        if (Api.Emulation.GetGameInfo() is { } gameInfo) {
            _version = _gameVersions.Get(gameInfo.Hash, Api.Memory);
        } else {
            _version = null;
        }
    }

    protected override void UpdateAfter() {
        if (_version is null) {
            return;
        }
        
        _roomLabel.Text = $"Room: {_version.RoomId}";
        _rngValueLabel.Text = $"{_version.RngState:X04}";
        _scriptRngValueLabel.Text = $"{_version.ScriptRngState:X04}";
        
        var scriptRngOffsetIndex = _version.ScriptRngOffsetIndex;
        var scriptRngOffset = _version.ScriptRngOffset;
        _scriptRngOffsetIndexLabel.Text = $"{scriptRngOffsetIndex} ({scriptRngOffsetIndex:X02})";
        _scriptRngOffsetLabel.Text = $"{scriptRngOffset} ({scriptRngOffset:X02})";
    }
}