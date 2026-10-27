Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Customer dialog to rate an item (1-5 stars) and leave an optional comment. The caller saves the result.</summary>
Public Class ReviewForm
    Inherits Form

    Public Rating As Integer
    Public Comment As String = ""
    Public Deleted As Boolean

    Private ReadOnly _stars As New StarPicker()
    Private ReadOnly _hint As Label
    Private ReadOnly _area As New AreaField()

    Public Sub New(productName As String, currentRating As Integer, currentComment As String, editing As Boolean)
        Text = If(editing, "Edit your review", "Write a review")
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

        Dim t As Label = Ui.Lbl(If(editing, "Edit your review", "Rate this item"), 16, True)
        t.Location = New Point(x, y)
        Controls.Add(t)
        y += 38
        Dim pn As Label = Ui.Lbl(Ui.Ellipsize(productName, 48), 10.5F, False, Theme.TextMuted)
        pn.Location = New Point(x, y)
        Controls.Add(pn)
        y += 36

        _stars.Location = New Point(x, y)
        _stars.Value = currentRating
        AddHandler _stars.ValueChanged, AddressOf OnStars
        Controls.Add(_stars)
        y += _stars.Height + 6
        _hint = Ui.Lbl("", 10, False, Theme.TextMuted)
        _hint.Location = New Point(x, y)
        Controls.Add(_hint)
        ShowHint()
        y += 34

        Dim cap As Label = Ui.Lbl("Comment (optional)", 10.5F, True)
        cap.Location = New Point(x, y)
        Controls.Add(cap)
        y += 28
        _area.MaxLen = 255
        _area.Value = currentComment
        _area.SetBounds(x, y, cw, 120)
        Controls.Add(_area)
        y += 120 + 24

        If editing Then
            Dim del As Label = Ui.Link("Delete my review", 10, Theme.Danger, False)
            del.Location = New Point(x, y + 14)
            AddHandler del.Click, AddressOf OnDelete
            Controls.Add(del)
        End If
        Dim save As RoundedButton = Ui.PrimaryBtn("Save review", 150, 46)
        save.Location = New Point(x + cw - 150, y)
        AddHandler save.Click, AddressOf OnSave
        Controls.Add(save)
        Dim cancel As RoundedButton = Ui.OutlineBtn("Cancel", 110, 46)
        cancel.Location = New Point(x + cw - 150 - 12 - 110, y)
        AddHandler cancel.Click, Sub(s, e) DialogResult = DialogResult.Cancel
        Controls.Add(cancel)

        ClientSize = New Size(fullW, y + 46 + 26)
    End Sub

    Private Sub ShowHint()
        Select Case _stars.Value
            Case 1 : _hint.Text = "Poor"
            Case 2 : _hint.Text = "Fair"
            Case 3 : _hint.Text = "Good"
            Case 4 : _hint.Text = "Very good"
            Case 5 : _hint.Text = "Excellent"
            Case Else : _hint.Text = "Tap a star to rate"
        End Select
    End Sub

    Private Sub OnStars(sender As Object, e As EventArgs)
        ShowHint()
    End Sub

    Private Sub OnSave(sender As Object, e As EventArgs)
        If _stars.Value < 1 Then
            Ui.Warn("Please choose a star rating.")
            Return
        End If
        Rating = _stars.Value
        Comment = _area.Value.Trim()
        DialogResult = DialogResult.OK
    End Sub

    Private Sub OnDelete(sender As Object, e As EventArgs)
        If Not Ui.Confirm("Delete your review? This cannot be undone.") Then Return
        Deleted = True
        DialogResult = DialogResult.OK
    End Sub
End Class
