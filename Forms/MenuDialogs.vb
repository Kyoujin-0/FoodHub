Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Asks for one line of text (used for new category names).</summary>
Public Class PromptForm
    Inherits Form

    Public Value As String = ""
    Private ReadOnly _box As New AppTextBox()

    Public Sub New(title As String, caption As String, placeholder As String, maxLen As Integer)
        Text = title
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        BackColor = Color.White
        Font = Theme.F(10)

        Dim fullW As Integer = 420
        Dim x As Integer = 28
        Dim cw As Integer = fullW - 56
        Dim y As Integer = 26

        Dim t As Label = Ui.Lbl(title, 16, True)
        t.Location = New Point(x, y)
        Controls.Add(t)
        y += 40
        Dim c As Label = Ui.Lbl(caption, 10.5F, True)
        c.Location = New Point(x, y)
        Controls.Add(c)
        y += 26
        _box.Font = Theme.F(11)
        _box.PlaceholderText = placeholder
        _box.MaxLength = maxLen
        _box.SetBounds(x, y, cw, 48)
        AddHandler _box.EnterPressed, AddressOf OnOk
        Controls.Add(_box)
        y += 48 + 24

        Dim cancel As RoundedButton = Ui.OutlineBtn("Cancel", 120, 46)
        cancel.Location = New Point(x, y)
        AddHandler cancel.Click, Sub(s, e) DialogResult = DialogResult.Cancel
        Controls.Add(cancel)
        Dim ok As RoundedButton = Ui.PrimaryBtn("Add", 150, 46)
        ok.Location = New Point(x + cw - 150, y)
        AddHandler ok.Click, AddressOf OnOk
        Controls.Add(ok)

        ClientSize = New Size(fullW, y + 46 + 26)
    End Sub

    Private Sub OnOk(sender As Object, e As EventArgs)
        Value = _box.Text.Trim()
        If Value = "" Then Return
        DialogResult = DialogResult.OK
    End Sub
End Class

''' <summary>Manage the stall's extras library (name + price). Items choose which extras they offer when you edit them.</summary>
Public Class ExtrasManagerForm
    Inherits Form

    Private Class ExtraEditRow
        Public Id As Integer
        Public NameBox As AppTextBox
        Public PriceBox As AppTextBox
        Public Del As IconButton
    End Class

    Private ReadOnly _stallId As Integer
    Private ReadOnly _list As New Panel()
    Private ReadOnly _rows As New List(Of ExtraEditRow)
    Private ReadOnly _empty As Label

    Public Sub New(stallId As Integer)
        _stallId = stallId
        Text = "Manage extras"
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        BackColor = Color.White
        Font = Theme.F(10)

        Dim fullW As Integer = 540
        Dim x As Integer = 28
        Dim cw As Integer = fullW - 56
        Dim y As Integer = 26

        Dim t As Label = Ui.Lbl("Manage extras", 16, True)
        t.Location = New Point(x, y)
        Controls.Add(t)
        y += 38
        Dim sub1 As Label = Ui.WrapLbl("These extras belong to your whole stall. When you add or edit an item, tick the extras that item offers. Changing a price here updates every item that uses it.", 10, cw, Theme.TextMuted)
        sub1.Location = New Point(x, y)
        Controls.Add(sub1)
        y += sub1.Height + 14

        _list.AutoScroll = True
        _list.BackColor = Color.White
        _list.SetBounds(x, y, cw, 300)
        Controls.Add(_list)
        y += 300 + 12

        _empty = Ui.WrapLbl("No extras yet. Click Add extra to create the first one.", 10, cw - 20, Theme.TextMuted)
        _empty.Location = New Point(4, 8)
        _list.Controls.Add(_empty)

        Dim addBtn As RoundedButton = Ui.SoftBtn("+ Add extra", 150, 42)
        addBtn.Location = New Point(x, y)
        AddHandler addBtn.Click, AddressOf OnAddRow
        Controls.Add(addBtn)
        y += 42 + 24

        Dim cancel As RoundedButton = Ui.OutlineBtn("Cancel", 120, 46)
        cancel.Location = New Point(x, y)
        AddHandler cancel.Click, Sub(s, e) DialogResult = DialogResult.Cancel
        Controls.Add(cancel)
        Dim save As RoundedButton = Ui.PrimaryBtn("Save extras", 170, 46)
        save.Location = New Point(x + cw - 170, y)
        AddHandler save.Click, AddressOf OnSave
        Controls.Add(save)

        ClientSize = New Size(fullW, y + 46 + 26)

        Try
            For Each r As DataRow In SellerSvc.GetStallExtras(stallId).Rows
                AddRow(Db.GetInt(r, "id"), Db.GetStr(r, "name"), MenuFmt.FormatNum(Db.GetDec(r, "price")))
            Next
        Catch ex As Exception
            Ui.Warn("Could not load your extras: " & ex.Message)
        End Try
        LayoutRows()
    End Sub

    Private Function AddRow(id As Integer, name As String, price As String) As ExtraEditRow
        Dim r As New ExtraEditRow()
        r.Id = id
        r.NameBox = New AppTextBox()
        r.NameBox.Font = Theme.F(11)
        r.NameBox.PlaceholderText = "Extra name, e.g. Pearls"
        r.NameBox.MaxLength = 40
        r.NameBox.Text = name
        r.PriceBox = New AppTextBox()
        r.PriceBox.Font = Theme.F(11)
        r.PriceBox.PlaceholderText = "Price"
        r.PriceBox.MaxLength = 7
        MenuFmt.NumericOnly(r.PriceBox, True)
        r.PriceBox.Text = price
        r.Del = New IconButton()
        r.Del.IconName = "x"
        r.Del.IconColor = Theme.TextMuted
        r.Del.BorderColor = Theme.Border
        r.Del.HoverBack = Theme.DangerSoft
        r.Del.Size = New Size(44, 44)
        r.Del.BackColor = Color.White
        r.Del.Tag = r
        AddHandler r.Del.Click, AddressOf OnDeleteRow
        _list.Controls.Add(r.NameBox)
        _list.Controls.Add(r.PriceBox)
        _list.Controls.Add(r.Del)
        _rows.Add(r)
        Return r
    End Function

    Private Sub LayoutRows()
        Dim pos As Integer = -_list.AutoScrollPosition.Y
        _list.AutoScrollPosition = New Point(0, 0)
        Dim y As Integer = 0
        For Each r As ExtraEditRow In _rows
            r.NameBox.SetBounds(0, y, 260, 46)
            r.PriceBox.SetBounds(268, y, 110, 46)
            r.Del.SetBounds(386, y + 1, 44, 44)
            y += 54
        Next
        _empty.Visible = (_rows.Count = 0)
        _list.AutoScrollMinSize = New Size(0, y)
        _list.AutoScrollPosition = New Point(0, pos)
    End Sub

    Private Sub OnAddRow(sender As Object, e As EventArgs)
        If _rows.Count >= 40 Then
            Ui.Warn("You can have up to 40 extras.")
            Return
        End If
        Dim r As ExtraEditRow = AddRow(0, "", "")
        LayoutRows()
        _list.ScrollControlIntoView(r.NameBox)
        r.NameBox.Focus()
    End Sub

    Private Sub OnDeleteRow(sender As Object, e As EventArgs)
        Dim r As ExtraEditRow = DirectCast(DirectCast(sender, Control).Tag, ExtraEditRow)
        If r.Id > 0 Then
            If Not Ui.Confirm("Delete this extra? It will be removed from every item that offers it when you save.") Then Return
        End If
        For Each c As Control In New Control() {r.NameBox, r.PriceBox, r.Del}
            _list.Controls.Remove(c)
            c.Dispose()
        Next
        _rows.Remove(r)
        LayoutRows()
    End Sub

    Private Sub OnSave(sender As Object, e As EventArgs)
        Dim rows As New List(Of ExtraRow)
        Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each r As ExtraEditRow In _rows
            Dim er As New ExtraRow()
            er.Id = r.Id
            er.Name = r.NameBox.Text.Trim()
            If er.Name = "" Then
                Ui.Warn("Every extra needs a name.")
                r.NameBox.Focus()
                Return
            End If
            If Not names.Add(er.Name) Then
                Ui.Warn("Extra names must be different: " & er.Name)
                r.NameBox.Focus()
                Return
            End If
            Dim p As Decimal
            If Not MenuFmt.TryPrice(r.PriceBox.Text, p) Then
                Ui.Warn("Enter a price for " & er.Name & " (0 for free).")
                r.PriceBox.Focus()
                Return
            End If
            er.Price = p
            rows.Add(er)
        Next
        Dim err As String = ""
        If Not SellerSvc.SaveStallExtras(_stallId, rows, err) Then
            Ui.Warn(err)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub
End Class
