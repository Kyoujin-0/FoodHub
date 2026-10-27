Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Colours and small drawing helpers shared by the admin Rent controls.</summary>
Public Module RentStyle
    Public Const TxtLeft As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix
    Public Const TxtRight As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.Right
    Public Const TxtCenter As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.NoPrefix

    Public ReadOnly Dot As String = " " & ChrW(183) & " "

    Public Function RentFore(key As String) As Color
        Select Case key
            Case "paid" : Return Theme.Success
            Case "overdue" : Return Theme.Danger
            Case "partial" : Return Color.FromArgb(37, 99, 235)
            Case Else : Return Color.FromArgb(161, 98, 7)
        End Select
    End Function

    Public Function RentBack(key As String) As Color
        Select Case key
            Case "paid" : Return Theme.SuccessSoft
            Case "overdue" : Return Theme.DangerSoft
            Case "partial" : Return Color.FromArgb(224, 236, 255)
            Case Else : Return Color.FromArgb(254, 243, 199)
        End Select
    End Function

    Public Function TextW(t As String, f As Font) As Integer
        Return TextRenderer.MeasureText(t, f, New Size(2000, 100), TextFormatFlags.NoPadding).Width
    End Function

    Public Function Initials(name As String) As String
        Dim parts() As String = name.Trim().Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length = 0 Then Return "?"
        If parts.Length = 1 Then Return parts(0).Substring(0, 1).ToUpperInvariant()
        Return (parts(0).Substring(0, 1) & parts(1).Substring(0, 1)).ToUpperInvariant()
    End Function

    Public Sub DrawPill(g As Graphics, r As Rectangle, text As String, fore As Color, back As Color, f As Font)
        Using p As GraphicsPath = Theme.RoundRectPath(r, r.Height \ 2), b As New SolidBrush(back)
            g.FillPath(b, p)
        End Using
        TextRenderer.DrawText(g, text, f, r, fore, TxtCenter)
    End Sub

    Public Sub DrawCircleInitials(g As Graphics, r As Rectangle, name As String, f As Font)
        Using b As New SolidBrush(Theme.PrimarySoft)
            g.FillEllipse(b, r)
        End Using
        TextRenderer.DrawText(g, Initials(name), f, r, Theme.Primary, TxtCenter)
    End Sub

    ''' <summary>The seller's photo in a circle; the initials circle when there is no photo.</summary>
    Public Sub DrawPhotoOrInitials(g As Graphics, r As Rectangle, name As String, photoPath As String, f As Font)
        Dim img As Image = Nothing
        If If(photoPath, "") <> "" Then img = Avatars.Load(photoPath)
        If img IsNot Nothing Then
            Avatars.DrawImageCircle(g, r, img)
        Else
            DrawCircleInitials(g, r, name, f)
        End If
    End Sub
End Module

''' <summary>Small summary tile (caption + big value).</summary>
Public Class StatCard
    Inherits Control

    Private _caption As String = ""
    Private _value As String = ""
    Private _color As Color = Theme.TextDark
    Private ReadOnly _fCap As Font = Theme.F(10.5F, True)
    Private ReadOnly _fVal As Font = Theme.F(24, True)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg
    End Sub

    Public Sub SetValue(caption As String, value As String, color As Color)
        _caption = caption
        _value = value
        _color = color
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), 22)
            g.FillPath(Brushes.White, p)
            Using pen As New Pen(Color.FromArgb(240, 230, 220), 1.0F)
                g.DrawPath(pen, p)
            End Using
        End Using
        TextRenderer.DrawText(g, _caption, _fCap, New Rectangle(22, 16, Width - 44, 24), Theme.TextMuted, TxtLeft)
        TextRenderer.DrawText(g, _value, _fVal, New Rectangle(22, 44, Width - 44, 50), _color, TxtLeft)
    End Sub
End Class

''' <summary>White pill with "&lt;  October 2026  &gt;".</summary>
Public Class MonthPicker
    Inherits Control

    Public Event PrevClicked()
    Public Event NextClicked()

    Private _text As String = ""
    Private _canPrev As Boolean
    Private _canNext As Boolean
    Private _hot As Integer
    Private ReadOnly _f As Font = Theme.F(12, True)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg
    End Sub

    Public Sub Setup(month As Date, canPrev As Boolean, canNext As Boolean)
        _text = month.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
        _canPrev = canPrev
        _canNext = canNext
        Invalidate()
    End Sub

    Private Function PrevRect() As Rectangle
        Return New Rectangle(8, 6, 40, Height - 12)
    End Function

    Private Function NextRect() As Rectangle
        Return New Rectangle(Width - 48, 6, 40, Height - 12)
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), Height \ 2)
            g.FillPath(Brushes.White, p)
            Using pen As New Pen(Theme.Border, 1.5F)
                g.DrawPath(pen, p)
            End Using
        End Using
        Dim pc As Color = If(_canPrev, Theme.TextDark, Theme.Border)
        Dim nc As Color = If(_canNext, Theme.TextDark, Theme.Border)
        Dim pr As Rectangle = PrevRect()
        Dim nr As Rectangle = NextRect()
        Icons.Draw(g, "chev_l", New Rectangle(pr.X + 11, pr.Y + (pr.Height - 18) \ 2, 18, 18), pc)
        Icons.Draw(g, "chev_r", New Rectangle(nr.X + 11, nr.Y + (nr.Height - 18) \ 2, 18, 18), nc)
        TextRenderer.DrawText(g, _text, _f, New Rectangle(52, 0, Width - 104, Height), Theme.TextDark, TxtCenter)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim h As Integer = 0
        If _canPrev AndAlso PrevRect().Contains(e.Location) Then h = 1
        If _canNext AndAlso NextRect().Contains(e.Location) Then h = 2
        If h <> _hot Then
            _hot = h
            Cursor = If(h > 0, Cursors.Hand, Cursors.Default)
        End If
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        If _canPrev AndAlso PrevRect().Contains(e.Location) Then
            RaiseEvent PrevClicked()
        ElseIf _canNext AndAlso NextRect().Contains(e.Location) Then
            RaiseEvent NextClicked()
        End If
    End Sub
End Class

''' <summary>"Rent: P5,000" pill, click to change the flat monthly rent.</summary>
Public Class RentPill
    Inherits Control

    Public Event Clicked()
    Private _text As String = ""
    Private ReadOnly _f As Font = Theme.F(12, True)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg
        Cursor = Cursors.Hand
    End Sub

    Public Sub SetText(t As String)
        _text = t
        Invalidate()
    End Sub

    Public Function PreferredWidth() As Integer
        Return TextW(_text, _f) + 48
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), Height \ 2)
            g.FillPath(Brushes.White, p)
            Using pen As New Pen(Theme.Border, 1.5F)
                g.DrawPath(pen, p)
            End Using
        End Using
        TextRenderer.DrawText(g, _text, _f, New Rectangle(0, 0, Width, Height), Theme.TextDark, TxtCenter)
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        RaiseEvent Clicked()
    End Sub
End Class

''' <summary>White rounded card that holds other controls.</summary>
Public Class CardPanel
    Inherits Panel

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(Theme.Bg)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), 26)
            g.FillPath(Brushes.White, p)
            Using pen As New Pen(Color.FromArgb(240, 230, 220), 1.0F)
                g.DrawPath(pen, p)
            End Using
        End Using
    End Sub
End Class

Public Class RentListItem
    Public Key As Integer
    Public StallId As Integer
    Public PhotoPath As String = ""
    Public Title As String = ""
    Public SubText As String = ""
    Public Amount As String = ""
    Public AmountSub As String = ""
    Public PillText As String = ""
    Public PillKey As String = ""
End Class

''' <summary>Painted list of stalls (or payments). The page scrolls, so the control is as tall as all its rows.</summary>
Public Class RentList
    Inherits Control

    Public Const RowH As Integer = 78
    Public Event SelectionChanged()
    Public EmptyText As String = "Nothing to show."

    Private ReadOnly _items As New List(Of RentListItem)
    Private _selected As Integer = -1
    Private _hot As Integer = -1
    Private ReadOnly _fTitle As Font = Theme.F(12, True)
    Private ReadOnly _fSub As Font = Theme.F(10)
    Private ReadOnly _fAmount As Font = Theme.F(12, True)
    Private ReadOnly _fPill As Font = Theme.F(10, True)
    Private ReadOnly _fInit As Font = Theme.F(12, True)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.White
    End Sub

    Public ReadOnly Property SelectedKey As Integer
        Get
            If _selected >= 0 AndAlso _selected < _items.Count Then Return _items(_selected).Key
            Return 0
        End Get
    End Property

    ''' <summary>Replaces the rows. Keeps the row with selectKey selected; otherwise the first row when autoSelect is on.</summary>
    Public Sub SetItems(items As List(Of RentListItem), selectKey As Integer, autoSelect As Boolean)
        _items.Clear()
        _items.AddRange(items)
        _selected = -1
        For i As Integer = 0 To _items.Count - 1
            If selectKey <> 0 AndAlso _items(i).Key = selectKey Then
                _selected = i
                Exit For
            End If
        Next
        If _selected < 0 AndAlso autoSelect AndAlso _items.Count > 0 Then _selected = 0
        _hot = -1
        Invalidate()
    End Sub

    Public Function DesiredHeight() As Integer
        Return Math.Max(RowH * 2, _items.Count * RowH)
    End Function

    ''' <summary>Updates the photos of the rows (stall id -> photo path) and repaints only when something changed.</summary>
    Public Sub UpdatePhotos(map As Dictionary(Of Integer, String))
        Dim changed As Boolean = False
        For Each it As RentListItem In _items
            Dim p As String = ""
            If map.TryGetValue(it.StallId, p) AndAlso p <> it.PhotoPath Then
                it.PhotoPath = p
                changed = True
            End If
        Next
        If changed Then Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(Color.White)
        g.SmoothingMode = SmoothingMode.AntiAlias
        If _items.Count = 0 Then
            TextRenderer.DrawText(g, EmptyText, _fTitle, New Rectangle(0, 0, Width, RowH * 2), Theme.TextMuted, TxtCenter)
            Return
        End If
        For i As Integer = 0 To _items.Count - 1
            Dim r As New Rectangle(0, i * RowH, Width, RowH)
            If r.Bottom < e.ClipRectangle.Top OrElse r.Top > e.ClipRectangle.Bottom Then Continue For
            Dim it As RentListItem = _items(i)
            If i = _selected Then
                Using b As New SolidBrush(Theme.PrimarySoft)
                    g.FillRectangle(b, r)
                End Using
                Using b As New SolidBrush(Theme.Primary)
                    g.FillRectangle(b, r.X, r.Y, 4, r.Height)
                End Using
            ElseIf i = _hot Then
                Using b As New SolidBrush(Color.FromArgb(253, 249, 245))
                    g.FillRectangle(b, r)
                End Using
            End If
            Using pen As New Pen(Color.FromArgb(240, 232, 224), 1.0F)
                g.DrawLine(pen, r.X, r.Bottom - 1, r.Right, r.Bottom - 1)
            End Using

            DrawPhotoOrInitials(g, New Rectangle(26, r.Y + (RowH - 44) \ 2, 44, 44), it.Title, it.PhotoPath, _fInit)

            Dim rightX As Integer = Width - 24
            If it.PillText <> "" Then
                Dim pw As Integer = TextW(it.PillText, _fPill) + 28
                DrawPill(g, New Rectangle(rightX - pw, r.Y + (RowH - 28) \ 2, pw, 28), it.PillText, RentFore(it.PillKey), RentBack(it.PillKey), _fPill)
                rightX -= pw + 14
            End If
            Dim amtW As Integer = Math.Max(TextW(it.Amount, _fAmount), TextW(it.AmountSub, _fSub)) + 6
            TextRenderer.DrawText(g, it.Amount, _fAmount, New Rectangle(rightX - amtW, r.Y + 14, amtW, 24), Theme.TextDark, TxtRight)
            TextRenderer.DrawText(g, it.AmountSub, _fSub, New Rectangle(rightX - amtW, r.Y + 40, amtW, 22), Theme.TextMuted, TxtRight)

            Dim textW2 As Integer = Math.Max(60, rightX - amtW - 16 - 86)
            TextRenderer.DrawText(g, it.Title, _fTitle, New Rectangle(86, r.Y + 14, textW2, 24), Theme.TextDark, TxtLeft)
            TextRenderer.DrawText(g, it.SubText, _fSub, New Rectangle(86, r.Y + 40, textW2, 22), Theme.TextMuted, TxtLeft)
        Next
    End Sub

    Private Function RowAt(y As Integer) As Integer
        Dim i As Integer = y \ RowH
        If i >= 0 AndAlso i < _items.Count Then Return i
        Return -1
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim h As Integer = RowAt(e.Y)
        If h <> _hot Then
            _hot = h
            Cursor = If(h >= 0, Cursors.Hand, Cursors.Default)
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hot <> -1 Then
            _hot = -1
            Cursor = Cursors.Default
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        Dim i As Integer = RowAt(e.Y)
        If i >= 0 AndAlso i <> _selected Then
            _selected = i
            Invalidate()
            RaiseEvent SelectionChanged()
        End If
    End Sub
End Class

''' <summary>Right-hand "Record cash payment" card for the selected stall.</summary>
Public Class RentPanel
    Inherits Control

    Public Event RecordRequested(amount As Decimal)
    Public Event ChangeDueRequested()

    Public Const PanelH As Integer = 420

    Private ReadOnly _amount As New AppTextBox()
    Private _row As RentRow
    Private _month As Date
    Private _btnRecord As Rectangle
    Private _btnFull As Rectangle
    Private _linkDue As Rectangle
    Private _hot As Integer
    Private ReadOnly _fTitle As Font = Theme.F(16, True)
    Private ReadOnly _fName As Font = Theme.F(14, True)
    Private ReadOnly _fSub As Font = Theme.F(10.5F)
    Private ReadOnly _fCap As Font = Theme.F(9.5F)
    Private ReadOnly _fVal As Font = Theme.F(13, True)
    Private ReadOnly _fBtn As Font = Theme.F(11, True)
    Private ReadOnly _fPill As Font = Theme.F(10, True)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg
        _amount.PlaceholderText = "Amount to record"
        _amount.Font = Theme.F(11)
        _amount.MaxLength = 9
        _amount.BackColor = Color.White
        _amount.Visible = False
        Controls.Add(_amount)
        AddHandler _amount.Inner.KeyPress, AddressOf OnAmountKey
        AddHandler _amount.EnterPressed, Sub(s, e) DoRecord()
    End Sub

    Private Sub OnAmountKey(sender As Object, e As KeyPressEventArgs)
        If Char.IsControl(e.KeyChar) OrElse Char.IsDigit(e.KeyChar) Then Return
        If e.KeyChar = "."c AndAlso Not DirectCast(sender, TextBox).Text.Contains(".") Then Return
        e.Handled = True
    End Sub

    ''' <summary>Repaints after the photo path of the shown row changed.</summary>
    Public Sub RefreshPhoto()
        Invalidate()
    End Sub

    Public Sub ShowStall(row As RentRow, month As Date)
        _row = row
        _month = month
        If row IsNot Nothing AndAlso row.Balance > 0D Then
            _amount.Text = row.Balance.ToString("0.##", CultureInfo.InvariantCulture)
        Else
            _amount.Text = ""
        End If
        LayoutInner()
        Invalidate()
    End Sub

    Private Sub DoRecord()
        If _row Is Nothing Then Return
        Dim v As Decimal
        If Not Decimal.TryParse(_amount.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, v) Then v = 0D
        RaiseEvent RecordRequested(v)
    End Sub

    Private Sub LayoutInner()
        Dim w As Integer = Width
        _linkDue = New Rectangle(w - 28 - 74, 244, 74, 24)
        If _row IsNot Nothing AndAlso _row.Balance > 0D Then
            _amount.SetBounds(28, 288, w - 56, 50)
            _amount.Visible = True
            Dim recW As Integer = CInt((w - 56) * 0.56)
            _btnRecord = New Rectangle(28, 354, recW, 46)
            _btnFull = New Rectangle(28 + recW + 10, 354, w - 56 - recW - 10, 46)
        Else
            _amount.Visible = False
            _btnRecord = Rectangle.Empty
            _btnFull = Rectangle.Empty
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        LayoutInner()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim w As Integer = Width
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, w - 1, Height - 1), 26)
            g.FillPath(Brushes.White, p)
            Using pen As New Pen(Color.FromArgb(240, 230, 220), 1.0F)
                g.DrawPath(pen, p)
            End Using
        End Using
        TextRenderer.DrawText(g, "Record cash payment", _fTitle, New Rectangle(28, 24, w - 56, 34), Theme.TextDark, TxtLeft)
        If _row Is Nothing Then
            TextRenderer.DrawText(g, "Select a stall from the list.", _fSub, New Rectangle(28, 76, w - 56, 26), Theme.TextMuted, TxtLeft)
            Return
        End If

        ' stall header
        DrawPhotoOrInitials(g, New Rectangle(28, 78, 52, 52), _row.StallName, _row.PhotoPath, _fName)
        Dim pillText As String = RentStatusText(_row.Status)
        Dim pw As Integer = TextW(pillText, _fPill) + 28
        DrawPill(g, New Rectangle(w - 28 - pw, 90, pw, 28), pillText, RentFore(_row.Status), RentBack(_row.Status), _fPill)
        Dim nameW As Integer = w - 28 - 92 - pw - 12
        TextRenderer.DrawText(g, _row.StallName, _fName, New Rectangle(92, 80, nameW, 26), Theme.TextDark, TxtLeft)
        TextRenderer.DrawText(g, _row.Owner, _fSub, New Rectangle(92, 106, nameW, 22), Theme.TextMuted, TxtLeft)

        ' Rent / Paid / Balance tiles
        Dim tw As Integer = (w - 56 - 20) \ 3
        Dim caps() As String = {"Rent", "Paid", "Balance"}
        Dim vals() As String = {Money(_row.Rent), Money(_row.Paid), Money(_row.Balance)}
        For i As Integer = 0 To 2
            Dim tr As New Rectangle(28 + i * (tw + 10), 150, tw, 76)
            Using p As GraphicsPath = Theme.RoundRectPath(tr, 16), b As New SolidBrush(Color.FromArgb(252, 248, 243))
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, caps(i), _fCap, New Rectangle(tr.X + 14, tr.Y + 10, tr.Width - 20, 20), Theme.TextMuted, TxtLeft)
            TextRenderer.DrawText(g, vals(i), _fVal, New Rectangle(tr.X + 14, tr.Y + 34, tr.Width - 20, 30), Theme.TextDark, TxtLeft)
        Next

        ' due date line
        Dim dueColor As Color = If(_row.Status = "overdue", Theme.Danger, Theme.TextMuted)
        TextRenderer.DrawText(g, "Due " & _row.DueDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture), _fSub,
                              New Rectangle(28, 244, w - 56 - 80, 24), dueColor, TxtLeft)
        TextRenderer.DrawText(g, "Change", _fBtn, _linkDue, Theme.Primary, TxtRight)

        If _row.Balance > 0D Then
            Using p As GraphicsPath = Theme.RoundRectPath(_btnRecord, 23), b As New SolidBrush(If(_hot = 1, Theme.PrimaryDark, Theme.Primary))
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, "Record payment", _fBtn, _btnRecord, Color.White, TxtCenter)
            Using p As GraphicsPath = Theme.RoundRectPath(_btnFull, 23)
                If _hot = 2 Then
                    Using b As New SolidBrush(Theme.PrimarySoft)
                        g.FillPath(b, p)
                    End Using
                End If
                Using pen As New Pen(Theme.Primary, 1.5F)
                    g.DrawPath(pen, p)
                End Using
            End Using
            TextRenderer.DrawText(g, "Pay full balance", _fBtn, _btnFull, Theme.Primary, TxtCenter)
        Else
            Dim br As New Rectangle(28, 288, w - 56, 84)
            Using p As GraphicsPath = Theme.RoundRectPath(br, 18), b As New SolidBrush(Theme.SuccessSoft)
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, "Fully paid for " & _month.ToString("MMMM yyyy", CultureInfo.InvariantCulture), _fName, br, Theme.Success, TxtCenter)
        End If
    End Sub

    Private Function HitTest(p As Point) As Integer
        If _row Is Nothing Then Return 0
        If Not _btnRecord.IsEmpty AndAlso _btnRecord.Contains(p) Then Return 1
        If Not _btnFull.IsEmpty AndAlso _btnFull.Contains(p) Then Return 2
        If _linkDue.Contains(p) Then Return 3
        Return 0
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim h As Integer = HitTest(e.Location)
        If h <> _hot Then
            _hot = h
            Cursor = If(h > 0, Cursors.Hand, Cursors.Default)
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hot <> 0 Then
            _hot = 0
            Cursor = Cursors.Default
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        Select Case HitTest(e.Location)
            Case 1 : DoRecord()
            Case 2 : If _row IsNot Nothing Then RaiseEvent RecordRequested(_row.Balance)
            Case 3 : RaiseEvent ChangeDueRequested()
        End Select
    End Sub
End Class

''' <summary>Tiny dialog for one number (monthly rent) or one date (due date).</summary>
Public Class RentDialog
    Inherits Form

    Public AmountValue As Decimal
    Public DateValue As Date

    Private ReadOnly _isDate As Boolean
    Private ReadOnly _amountBox As TextBox
    Private ReadOnly _datePick As DateTimePicker
    Private ReadOnly _err As Label

    Public Sub New(title As String, caption As String, isDate As Boolean, initialAmount As Decimal, initialDate As Date)
        _isDate = isDate
        Text = title
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(400, 210)
        BackColor = Color.White
        Font = Theme.F(10)

        Dim cap As New Label()
        cap.Text = caption
        cap.AutoSize = False
        cap.SetBounds(24, 20, 352, 44)
        cap.ForeColor = Theme.TextDark
        cap.Font = Theme.F(11, True)
        Controls.Add(cap)

        If isDate Then
            _datePick = New DateTimePicker()
            _datePick.Format = DateTimePickerFormat.Long
            Dim d As Date = initialDate
            If d < _datePick.MinDate Then d = _datePick.MinDate
            _datePick.Value = d
            _datePick.Font = Theme.F(12)
            _datePick.SetBounds(24, 74, 352, 32)
            Controls.Add(_datePick)
        Else
            _amountBox = New TextBox()
            _amountBox.Text = initialAmount.ToString("0.##", CultureInfo.InvariantCulture)
            _amountBox.Font = Theme.F(12)
            _amountBox.MaxLength = 9
            _amountBox.SetBounds(24, 74, 352, 32)
            AddHandler _amountBox.KeyPress, AddressOf OnAmountKey
            Controls.Add(_amountBox)
        End If

        _err = New Label()
        _err.AutoSize = False
        _err.SetBounds(24, 112, 352, 22)
        _err.ForeColor = Theme.Danger
        Controls.Add(_err)

        Dim ok As New Button()
        ok.Text = "Save"
        ok.FlatStyle = FlatStyle.Flat
        ok.FlatAppearance.BorderSize = 0
        ok.BackColor = Theme.Primary
        ok.ForeColor = Color.White
        ok.Font = Theme.F(11, True)
        ok.SetBounds(196, 150, 86, 40)
        AddHandler ok.Click, AddressOf OnOk
        Controls.Add(ok)

        Dim cancel As New Button()
        cancel.Text = "Cancel"
        cancel.FlatStyle = FlatStyle.Flat
        cancel.FlatAppearance.BorderColor = Theme.Border
        cancel.BackColor = Color.White
        cancel.ForeColor = Theme.TextDark
        cancel.Font = Theme.F(11)
        cancel.DialogResult = DialogResult.Cancel
        cancel.SetBounds(290, 150, 86, 40)
        Controls.Add(cancel)

        AcceptButton = ok
        CancelButton = cancel
    End Sub

    Private Sub OnAmountKey(sender As Object, e As KeyPressEventArgs)
        If Char.IsControl(e.KeyChar) OrElse Char.IsDigit(e.KeyChar) Then Return
        If e.KeyChar = "."c AndAlso Not _amountBox.Text.Contains(".") Then Return
        e.Handled = True
    End Sub

    Private Sub OnOk(sender As Object, e As EventArgs)
        If _isDate Then
            DateValue = _datePick.Value.Date
        Else
            Dim v As Decimal
            If Not Decimal.TryParse(_amountBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, v) OrElse v <= 0D Then
                _err.Text = "Enter an amount greater than zero."
                Return
            End If
            AmountValue = v
        End If
        DialogResult = DialogResult.OK
    End Sub
End Class

''' <summary>Admin Rent tab: month, stats, stall list with filters and the Record cash payment card. History shows the payments of the month.</summary>
Public Class AdminRentPage
    Inherits PageBase

    Private ReadOnly _accent As New Panel()
    Private ReadOnly _title As Label
    Private ReadOnly _sub As Label
    Private ReadOnly _month As New MonthPicker()
    Private ReadOnly _mode As New SegmentTabs()
    Private ReadOnly _pill As New RentPill()
    Private ReadOnly _cCollected As New StatCard()
    Private ReadOnly _cUnpaid As New StatCard()
    Private ReadOnly _cPartial As New StatCard()
    Private ReadOnly _cOverdue As New StatCard()
    Private ReadOnly _card As New CardPanel()
    Private ReadOnly _search As New AppTextBox()
    Private ReadOnly _chips As New ChipBar()
    Private ReadOnly _list As New RentList()
    Private ReadOnly _panel As New RentPanel()
    Private ReadOnly _statusKeys() As String = {"", "paid", "partial", "duesoon", "overdue"}
    Private Const SubtitleText As String = "Cash payments, recorded by you. Flat monthly rent for every stall."

    Private _monthStart As Date = RentSvc.MonthStart(Date.Today)
    Private _minMonth As Date = Date.MinValue
    Private _rows As New List(Of RentRow)
    Private _payments As DataTable
    Private _history As Boolean
    Private _keepKey As Integer

    Public Sub New()
        _accent.BackColor = Theme.Primary
        _accent.Size = New Size(48, 5)
        _title = Ui.Lbl("Rent", 26, True)
        _sub = Ui.Lbl(SubtitleText, 12, False, Theme.TextMuted)

        _mode.SetItems(New String() {"Collect", "History"}, New Integer() {0, 0})
        _mode.Size = New Size(230, 46)
        _month.Size = New Size(280, 46)

        _search.LeftIcon = "search"
        _search.PlaceholderText = "Search stall or owner"
        _search.Font = Theme.F(11)
        _search.MaxLength = 40
        _search.BackColor = Color.White
        _chips.BackColor = Color.White
        _chips.SetItems(New String() {"All", "Paid", "Partial", "Due soon", "Overdue"}, 0)

        _card.Controls.Add(_search)
        _card.Controls.Add(_chips)
        _card.Controls.Add(_list)

        Controls.Add(_accent)
        Controls.Add(_title)
        Controls.Add(_sub)
        Controls.Add(_month)
        Controls.Add(_mode)
        Controls.Add(_pill)
        Controls.Add(_cCollected)
        Controls.Add(_cUnpaid)
        Controls.Add(_cPartial)
        Controls.Add(_cOverdue)
        Controls.Add(_card)
        Controls.Add(_panel)

        AddHandler _month.PrevClicked, Sub() ChangeMonth(-1)
        AddHandler _month.NextClicked, Sub() ChangeMonth(1)
        AddHandler _mode.SelectedChanged, AddressOf OnModeChanged
        AddHandler _pill.Clicked, AddressOf EditMonthlyRent
        AddHandler _chips.SelectedChanged, Sub(s, e) ApplyFilter()
        AddHandler _search.Inner.TextChanged, Sub(s, e) ApplyFilter()
        AddHandler _list.SelectionChanged, AddressOf ShowSelected
        AddHandler _panel.RecordRequested, AddressOf OnRecord
        AddHandler _panel.ChangeDueRequested, AddressOf EditDueDate

        SafeLoad()
        AddHandler _photoTimer.Tick, AddressOf RefreshPhotos
        _photoTimer.Start()
    End Sub

    ' Seller photos can change while this page is open: check every few seconds and repaint only when one changed.
    Private ReadOnly _photoTimer As New Timer() With {.Interval = 6000}

    Private Sub RefreshPhotos(sender As Object, e As EventArgs)
        Try
            Dim map As Dictionary(Of Integer, String) = RentSvc.GetPhotos()
            For Each r As RentRow In _rows
                Dim p As String = ""
                If map.TryGetValue(r.StallId, p) Then r.PhotoPath = p
            Next
            _list.UpdatePhotos(map)
            _panel.RefreshPhoto()
        Catch
        End Try
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _photoTimer.Stop()
            _photoTimer.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    ''' <summary>Selects a stall in the Collect list (used by the Stores tab). Call on a freshly created page.</summary>
    Public Sub SelectStall(stallId As Integer)
        If _history Then Return
        _keepKey = stallId
        ApplyFilter()
    End Sub

    ' ---------------- data ----------------
    Private Function MonthLabel() As String
        Return _monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
    End Function

    Private Sub SafeLoad()
        Try
            LoadData()
            _sub.Text = SubtitleText
            _sub.ForeColor = Theme.TextMuted
        Catch ex As Exception
            _sub.Text = "Could not load the rent data (did you run patch_rent.sql?): " & ex.Message
            _sub.ForeColor = Theme.Danger
        End Try
    End Sub

    Private Sub LoadData()
        If _minMonth = Date.MinValue Then _minMonth = RentSvc.EarliestMonth()
        If _monthStart < _minMonth Then _monthStart = _minMonth
        _rows = RentSvc.GetRows(_monthStart)
        _payments = RentSvc.GetPayments(_monthStart)
        _pill.SetText("Rent: " & Money(RentSvc.GetMonthlyRent()))
        _month.Setup(_monthStart, _monthStart > _minMonth, _monthStart < RentSvc.MonthStart(Date.Today))

        Dim collected As Decimal = 0D
        Dim unpaid As Decimal = 0D
        Dim partialCount As Integer = 0
        Dim overdueCount As Integer = 0
        For Each r As RentRow In _rows
            collected += r.Paid
            unpaid += r.Balance
            If r.Status = "partial" Then partialCount += 1
            If r.Status = "overdue" Then overdueCount += 1
        Next
        _cCollected.SetValue("Collected", Money(collected), Theme.Success)
        _cUnpaid.SetValue("Unpaid", Money(unpaid), Theme.TextDark)
        _cPartial.SetValue("Partial", partialCount.ToString(), Color.FromArgb(161, 98, 7))
        _cOverdue.SetValue("Overdue", overdueCount.ToString(), Theme.Danger)
        ApplyFilter()
    End Sub

    Private Sub ApplyFilter()
        Dim q As String = _search.Text.Trim().ToLowerInvariant()
        Dim items As New List(Of RentListItem)
        If _history Then
            If _payments IsNot Nothing Then
                For Each r As DataRow In _payments.Rows
                    Dim stall As String = Db.GetStr(r, "stall")
                    Dim owner As String = Db.GetStr(r, "owner")
                    If q <> "" AndAlso Not (stall.ToLowerInvariant().Contains(q) OrElse owner.ToLowerInvariant().Contains(q)) Then Continue For
                    Dim rec As String = Db.GetStr(r, "recorded_by")
                    Dim it As New RentListItem()
                    it.Key = Db.GetInt(r, "id")
                    it.StallId = Db.GetInt(r, "stall_id")
                    it.PhotoPath = RentSvc.PhotoOf(Db.GetStr(r, "avatar"), Db.GetStr(r, "logo"))
                    it.Title = stall
                    it.SubText = owner & Dot & "recorded by " & If(rec = "", "unknown", rec)
                    it.Amount = Money(Db.GetDec(r, "amount"))
                    it.AmountSub = Db.GetDate(r, "paid_at").ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture)
                    items.Add(it)
                Next
            End If
            _list.EmptyText = "No payments recorded for " & MonthLabel() & "."
            _list.SetItems(items, 0, False)
        Else
            Dim want As String = _statusKeys(Math.Max(0, Math.Min(4, _chips.SelectedIndex)))
            For Each r As RentRow In _rows
                If want <> "" AndAlso r.Status <> want Then Continue For
                If q <> "" AndAlso Not (r.StallName.ToLowerInvariant().Contains(q) OrElse r.Owner.ToLowerInvariant().Contains(q)) Then Continue For
                Dim it As New RentListItem()
                it.Key = r.StallId
                it.StallId = r.StallId
                it.PhotoPath = r.PhotoPath
                it.Title = r.StallName
                If r.Paid > 0D AndAlso r.LastPaid <> Date.MinValue Then
                    it.SubText = r.Owner & Dot & "Paid " & r.LastPaid.ToString("MMM d", CultureInfo.InvariantCulture)
                Else
                    it.SubText = r.Owner & Dot & "Due " & r.DueDate.ToString("MMM d", CultureInfo.InvariantCulture)
                End If
                it.Amount = Money(r.Rent)
                Select Case r.Status
                    Case "paid" : it.AmountSub = "paid"
                    Case "partial" : it.AmountSub = Money(r.Paid) & " paid"
                    Case "overdue" : it.AmountSub = If(r.Paid > 0D, Money(r.Balance) & " left", "unpaid")
                    Case Else : it.AmountSub = "unpaid"
                End Select
                it.PillText = RentStatusText(r.Status)
                it.PillKey = r.Status
                items.Add(it)
            Next
            _list.EmptyText = "No stalls match."
            _list.SetItems(items, _keepKey, True)
        End If
        ShowSelected()
        RelayoutKeepScroll()
    End Sub

    Private Function SelectedRow() As RentRow
        Dim key As Integer = _list.SelectedKey
        If key = 0 Then Return Nothing
        For Each r As RentRow In _rows
            If r.StallId = key Then Return r
        Next
        Return Nothing
    End Function

    Private Sub ShowSelected()
        If _history Then
            _panel.ShowStall(Nothing, _monthStart)
        Else
            _keepKey = _list.SelectedKey
            _panel.ShowStall(SelectedRow(), _monthStart)
        End If
    End Sub

    ' ---------------- actions ----------------
    Private Sub ChangeMonth(delta As Integer)
        _monthStart = _monthStart.AddMonths(delta)
        SafeLoad()
    End Sub

    Private Sub OnModeChanged(sender As Object, e As EventArgs)
        _history = (_mode.SelectedIndex = 1)
        ApplyFilter()
    End Sub

    Private Sub OnRecord(amount As Decimal)
        Dim row As RentRow = SelectedRow()
        If row Is Nothing Then Return
        If amount <= 0D Then
            Toast.Show(FindForm(), "Enter an amount greater than zero.", True)
            Return
        End If
        If Not Ui.Confirm("Record a cash payment of " & Money(amount) & " from " & row.StallName & " for " & MonthLabel() & "?") Then Return
        Dim err As String = ""
        If Not RentSvc.RecordPayment(row.StallId, _monthStart, amount, err) Then
            Toast.Show(FindForm(), err, True)
            Return
        End If
        Toast.Show(FindForm(), "Payment recorded")
        SafeLoad()
    End Sub

    Private Sub EditDueDate()
        Dim row As RentRow = SelectedRow()
        If row Is Nothing Then Return
        Using dlg As New RentDialog("Change due date", "Due date of " & row.StallName & " for " & MonthLabel() & vbCrLf & "(the other months follow the same day)", True, 0D, row.DueDate)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            Try
                RentSvc.SetFirstDueDate(row.StallId, dlg.DateValue.AddMonths(-row.DueOffsetMonths))
            Catch ex As Exception
                Toast.Show(FindForm(), "Could not save the due date: " & ex.Message, True)
                Return
            End Try
        End Using
        Toast.Show(FindForm(), "Due date updated")
        SafeLoad()
    End Sub

    Private Sub EditMonthlyRent()
        Dim current As Decimal = 5000D
        Try
            current = RentSvc.GetMonthlyRent()
        Catch
        End Try
        Using dlg As New RentDialog("Monthly rent", "Flat monthly rent for every stall (applies to all months)", False, current, Date.Today)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            Try
                RentSvc.SetMonthlyRent(dlg.AmountValue)
            Catch ex As Exception
                Toast.Show(FindForm(), "Could not save the rent: " & ex.Message, True)
                Return
            End Try
        End Using
        Toast.Show(FindForm(), "Monthly rent updated")
        SafeLoad()
    End Sub

    ' ---------------- layout ----------------
    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim right As Integer = M + contentWidth
        _accent.Location = New Point(M, 22)
        _title.Location = New Point(M, 34)
        _sub.Location = New Point(M, 88)

        Dim pw As Integer = _pill.PreferredWidth()
        _pill.SetBounds(right - pw, 44, pw, 46)
        _mode.SetBounds(_pill.Left - 14 - 230, 44, 230, 46)
        _month.SetBounds(_mode.Left - 14 - 280, 44, 280, 46)

        Dim cw As Integer = (contentWidth - 3 * 20) \ 4
        Dim cards() As StatCard = {_cCollected, _cUnpaid, _cPartial, _cOverdue}
        For i As Integer = 0 To 3
            cards(i).SetBounds(M + i * (cw + 20), 132, cw, 112)
        Next

        Dim top As Integer = 266
        Dim panelW As Integer = 370
        Dim gap As Integer = 24
        Dim cardW As Integer = If(_history, contentWidth, contentWidth - panelW - gap)

        Dim searchW As Integer
        If _history Then
            searchW = Math.Min(420, cardW - 44)
        Else
            searchW = Math.Max(200, CInt(cardW * 0.34))
        End If
        _search.SetBounds(22, 22, searchW, 50)
        _chips.Visible = Not _history
        If Not _history Then _chips.SetBounds(22 + searchW + 16, 25, Math.Max(100, cardW - 44 - searchW - 16), 44)
        Dim listH As Integer = _list.DesiredHeight()
        _list.SetBounds(0, 92, cardW, listH)
        Dim cardH As Integer = 92 + listH + 16
        _card.SetBounds(M, top, cardW, cardH)

        _panel.Visible = Not _history
        _panel.SetBounds(M + cardW + gap, top, panelW, RentPanel.PanelH)

        SetContentHeight(top + Math.Max(cardH, If(_history, 0, RentPanel.PanelH)) + 40)
    End Sub
End Class
