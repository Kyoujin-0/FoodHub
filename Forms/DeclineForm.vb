Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Asks the seller why an order is declined. Pick a suggestion or type your own; the reason is optional.</summary>
Public Class DeclineReasonForm
    Inherits Form

    Public Reason As String = ""
    Private ReadOnly _box As New AppTextBox()

    Public Sub New(orderId As Integer)
        Text = "Decline order"
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        BackColor = Color.White
        Font = Theme.F(10)

        Dim fullW As Integer = 460
        Dim x As Integer = 28
        Dim cw As Integer = fullW - 56
        Dim y As Integer = 26

        Dim t As Label = Ui.Lbl("Decline order #" & orderId.ToString(), 16, True)
        t.Location = New Point(x, y)
        Controls.Add(t)
        y += 36

        Dim sub1 As Label = Ui.WrapLbl("Choose a reason or type your own. This is optional. The customer will see it in their messages.", 10, cw, Theme.TextMuted)
        sub1.Location = New Point(x, y)
        Controls.Add(sub1)
        y += sub1.Height + 14

        Dim chipFont As Font = Theme.F(10)
        Dim cx As Integer = x
        For Each reasonText As String In New String() {"Out of stock", "Item no longer available", "Stall is closing soon", "Too many orders right now", "Kitchen problem"}
            Dim w As Integer = TextRenderer.MeasureText(reasonText, chipFont, New Size(600, 40), TextFormatFlags.NoPadding).Width + 32
            If cx + w > x + cw Then
                cx = x
                y += 42
            End If
            Dim chip As RoundedButton = Ui.SoftBtn(reasonText, w, 34)
            chip.Font = chipFont
            chip.Location = New Point(cx, y)
            AddHandler chip.Click, Sub(s, e) _box.Text = DirectCast(s, RoundedButton).Text
            Controls.Add(chip)
            cx += w + 8
        Next
        y += 34 + 18

        _box.Font = Theme.F(11)
        _box.PlaceholderText = "Or type your own reason (optional)"
        _box.MaxLength = 120
        _box.SetBounds(x, y, cw, 48)
        Controls.Add(_box)
        y += 48 + 24

        Dim cancel As RoundedButton = Ui.OutlineBtn("Cancel", 120, 46)
        cancel.Location = New Point(x, y)
        AddHandler cancel.Click, Sub(s, e) DialogResult = DialogResult.Cancel
        Controls.Add(cancel)

        Dim ok As RoundedButton = Ui.PrimaryBtn("Decline order", 180, 46)
        ok.FillColor = Theme.Danger
        ok.HoverColor = Color.FromArgb(190, 40, 40)
        ok.TextColor = Color.White
        ok.Location = New Point(x + cw - 180, y)
        AddHandler ok.Click, AddressOf OnDeclineClick
        Controls.Add(ok)

        ClientSize = New Size(fullW, y + 46 + 26)
    End Sub

    Private Sub OnDeclineClick(sender As Object, e As EventArgs)
        Reason = _box.Text.Trim()
        DialogResult = DialogResult.OK
    End Sub
End Class
