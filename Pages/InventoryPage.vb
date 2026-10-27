Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

' =====================================================================================
' Seller > Inventory tab.
'   Tracked items table (- [number] +), "Needs attention" card (restock to a number),
'   and a collapsible "Not tracked" card (Available / Sold out switch, "Track stock" button).
' Data comes from SellerSvc.GetInventory / AdjustStock / SetStock / StartTracking / SetAvailable.
' =====================================================================================

''' <summary>Shared drawing helpers and sizes for the inventory controls.</summary>
Public Module InvPaint
    Public Const StepperW As Integer = 188
    Public Const PadR As Integer = 22
    Public Const StatusW As Integer = 96
    Public Const RowH As Integer = 82

    ''' <summary>Left edge of the STATUS column inside a row that is w pixels wide.</summary>
    Public Function StatusX(w As Integer) As Integer
        Return w - PadR - StepperW - 36 - StatusW
    End Function

    ''' <summary>0 = in stock, 1 = low, 2 = out.</summary>
    Public Function StatusOf(stock As Integer, lowAt As Integer) As Integer
        If stock <= 0 Then Return 2
        If stock <= lowAt Then Return 1
        Return 0
    End Function

    Public Function PillWidth(text As String, f As Font) As Integer
        Return TextRenderer.MeasureText(text, f, New Size(400, 40), TextFormatFlags.NoPadding).Width + 26
    End Function

    Public Sub Pill(g As Graphics, text As String, r As Rectangle, fill As Color, fore As Color)
        Dim sm As SmoothingMode = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(r, r.Height \ 2), b As New SolidBrush(fill)
            g.FillPath(b, p)
        End Using
        g.SmoothingMode = sm
        TextRenderer.DrawText(g, text, Theme.F(9.5F, True), r, fore,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
    End Sub

    Public Sub StatusPill(g As Graphics, kind As Integer, x As Integer, cy As Integer)
        Dim t As String = "In stock"
        Dim fill As Color = Theme.SuccessSoft
        Dim fore As Color = Theme.Success
        If kind = 1 Then
            t = "Low"
            fill = Theme.PrimarySoft
            fore = Theme.PrimaryDark
        ElseIf kind = 2 Then
            t = "Out"
            fill = Theme.DangerSoft
            fore = Theme.Danger
        End If
        Dim w As Integer = PillWidth(t, Theme.F(9.5F, True))
        Pill(g, t, New Rectangle(x, cy - 13, w, 26), fill, fore)
    End Sub

    Public Sub DrawText(g As Graphics, text As String, f As Font, r As Rectangle, c As Color, Optional extra As TextFormatFlags = TextFormatFlags.Left)
        TextRenderer.DrawText(g, text, f, r, c, extra Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub

    Public Sub SoftCircleBtn(b As IconButton, iconName As String)
        b.IconName = iconName
        b.IconColor = Theme.TextMuted
        b.HoverIconColor = Theme.TextDark
        b.BackColor = Theme.FieldBg
        b.HoverBack = Theme.Border
        b.Size = New Size(36, 36)
        Using gp As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, 36, 36), 18)
            b.Region = New Region(gp)
        End Using
    End Sub
End Module

''' <summary>One tracked item: photo, name, category, status pill and the - [number] + stepper.</summary>
Public Class InvRow
    Inherits Control

    Public ReadOnly ProductId As Integer
    Public ReadOnly Stock As Integer
    Public Event Adjusted(row As InvRow, delta As Integer)
    Public Event TypedStock(row As InvRow, value As Integer)

    Private ReadOnly _name As String
    Private ReadOnly _category As String
    Private ReadOnly _kind As Integer
    Private ReadOnly _img As New CoverImage()
    Private ReadOnly _minus As New IconButton()
    Private ReadOnly _plus As New IconButton()
    Private ReadOnly _qty As New AppTextBox()
    Private _isLast As Boolean
    Private _committed As Boolean

    Public Sub New(productId As Integer, name As String, category As String, stock As Integer, lowAt As Integer, img As Image)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Me.ProductId = productId
        Me.Stock = stock
        _name = name
        _category = category
        _kind = InvPaint.StatusOf(stock, lowAt)
        BackColor = Color.White

        _img.Radius = 12
        _img.Image = img
        _img.PlaceholderText = name
        _img.BackColor = Color.White
        _img.Size = New Size(62, 62)

        For Each b As IconButton In New IconButton() {_minus, _plus}
            b.IconColor = Theme.TextDark
            b.HoverIconColor = Theme.Primary
            b.BorderColor = Theme.Border
            b.HoverBack = Theme.PrimarySoft
            b.BackColor = Color.White
            b.Size = New Size(40, 40)
        Next
        _minus.IconName = "minus"
        _plus.IconName = "plus"

        _qty.Font = Theme.F(13, True)
        _qty.MaxLength = 5
        MenuFmt.NumericOnly(_qty, False)
        _qty.Text = stock.ToString()
        _qty.Inner.TextAlign = HorizontalAlignment.Center
        _qty.BackColor = Color.White

        Controls.Add(_img)
        Controls.Add(_minus)
        Controls.Add(_plus)
        Controls.Add(_qty)

        AddHandler _minus.Click, Sub(s, e) RaiseEvent Adjusted(Me, -1)
        AddHandler _plus.Click, Sub(s, e) RaiseEvent Adjusted(Me, 1)
        AddHandler _qty.EnterPressed, Sub(s, e) CommitTyped()
        AddHandler _qty.Inner.Leave, Sub(s, e) CommitTyped()
        PlaceChildren()
    End Sub

    Public Property IsLast As Boolean
        Get
            Return _isLast
        End Get
        Set(value As Boolean)
            _isLast = value
            Invalidate()
        End Set
    End Property

    Private Sub CommitTyped()
        If _committed Then Return
        Dim v As Integer
        If Not MenuFmt.TryInt(_qty.Text, v, 0, 99999) Then
            _qty.Text = Stock.ToString()
            Return
        End If
        If v = Stock Then Return
        _committed = True
        RaiseEvent TypedStock(Me, v)
    End Sub

    Private Sub PlaceChildren()
        If _qty Is Nothing Then Return
        _img.Location = New Point(16, (Height - 62) \ 2)
        Dim sx As Integer = Width - InvPaint.PadR - InvPaint.StepperW
        _minus.Location = New Point(sx, (Height - 40) \ 2)
        _qty.SetBounds(sx + 48, (Height - 46) \ 2, 92, 46)
        _plus.Location = New Point(sx + InvPaint.StepperW - 40, (Height - 40) \ 2)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        PlaceChildren()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        Dim right As Integer = InvPaint.StatusX(Width) - 14
        InvPaint.DrawText(g, _name, Theme.F(12.5F, True), New Rectangle(96, Height \ 2 - 28, Math.Max(40, right - 96), 28), Theme.TextDark)
        InvPaint.DrawText(g, _category, Theme.F(10.5F), New Rectangle(96, Height \ 2, Math.Max(40, right - 96), 24), Theme.TextMuted)
        InvPaint.StatusPill(g, _kind, InvPaint.StatusX(Width), Height \ 2)
        If Not _isLast Then
            Using pen As New Pen(Theme.Border, 1)
                g.DrawLine(pen, 16, Height - 1, Width - 16, Height - 1)
            End Using
        End If
    End Sub
End Class

''' <summary>The big white card with the tracked items table.</summary>
Public Class InvTableCard
    Inherits RoundedPanel

    Public Const HeaderH As Integer = 52
    Public ReadOnly Rows As New List(Of InvRow)
    Public EmptyText As String = ""

    Public Sub New()
        Radius = 20
        Shadow = True
    End Sub

    Public Function HeightFor(n As Integer) As Integer
        Return HeaderH + If(n = 0, 84, n * InvPaint.RowH) + 14
    End Function

    Public Sub LayoutRows()
        Dim rw As Integer = Math.Max(100, Width - 20)
        For i As Integer = 0 To Rows.Count - 1
            Rows(i).SetBounds(10, HeaderH + i * InvPaint.RowH, rw, InvPaint.RowH)
            Rows(i).IsLast = (i = Rows.Count - 1)
        Next
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g As Graphics = e.Graphics
        Dim rw As Integer = Width - 20
        Dim f As Font = Theme.F(9, True)
        InvPaint.DrawText(g, "ITEM", f, New Rectangle(10 + 96, 14, 200, 26), Theme.TextMuted)
        InvPaint.DrawText(g, "STATUS", f, New Rectangle(10 + InvPaint.StatusX(rw), 14, InvPaint.StatusW, 26), Theme.TextMuted)
        InvPaint.DrawText(g, "IN STOCK", f, New Rectangle(10 + rw - InvPaint.PadR - InvPaint.StepperW, 14, InvPaint.StepperW, 26), Theme.TextMuted, TextFormatFlags.HorizontalCenter)
        Using pen As New Pen(Theme.Border, 1)
            g.DrawLine(pen, 26, HeaderH - 2, Width - 26, HeaderH - 2)
        End Using
        If Rows.Count = 0 AndAlso EmptyText <> "" Then
            InvPaint.DrawText(g, EmptyText, Theme.F(11), New Rectangle(26, HeaderH + 14, Width - 52, 50), Theme.TextMuted)
        End If
    End Sub
End Class

''' <summary>One low / out-of-stock item inside the "Needs attention" card, with its own "Restock to" box.</summary>
Public Class AttentionItem
    Inherits Control

    Public Const ItemH As Integer = 114
    Public ReadOnly ProductId As Integer
    Public ReadOnly ItemName As String
    Public Event SetClicked(item As AttentionItem, qty As Integer)

    Private ReadOnly _stock As Integer
    Private ReadOnly _box As New AppTextBox()
    Private ReadOnly _set As RoundedButton

    Public Sub New(productId As Integer, name As String, stock As Integer)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Me.ProductId = productId
        ItemName = name
        _stock = stock
        BackColor = Color.White

        _box.Font = Theme.F(11)
        _box.PlaceholderText = "e.g. 20"
        _box.MaxLength = 5
        MenuFmt.NumericOnly(_box, False)
        _box.BackColor = Color.White
        _set = Ui.PrimaryBtn("Set", 80, 46)
        _set.Radius = 23
        _set.Font = Theme.F(11.5F, True)
        Controls.Add(_box)
        Controls.Add(_set)
        AddHandler _set.Click, Sub(s, e) DoSet()
        AddHandler _box.EnterPressed, Sub(s, e) DoSet()
        PlaceChildren()
    End Sub

    Private Sub DoSet()
        Dim v As Integer
        If Not MenuFmt.TryInt(_box.Text, v, 1, 99999) Then
            Toast.Show(Me, "Type the number to restock to, e.g. 20", True)
            Return
        End If
        RaiseEvent SetClicked(Me, v)
    End Sub

    Private Sub PlaceChildren()
        If _set Is Nothing Then Return
        _set.Location = New Point(Width - 80, 58)
        _box.SetBounds(104, 58, Math.Max(60, Width - 104 - 80 - 10), 46)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        PlaceChildren()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        Using pen As New Pen(Theme.Border, 1)
            g.DrawLine(pen, 0, 0, Width, 0)
        End Using
        Dim t As String = If(_stock <= 0, "Sold out", _stock.ToString() & " left")
        Dim pw As Integer = InvPaint.PillWidth(t, Theme.F(9.5F, True))
        InvPaint.Pill(g, t, New Rectangle(Width - pw, 16, pw, 26), If(_stock <= 0, Theme.DangerSoft, Theme.PrimarySoft), If(_stock <= 0, Theme.Danger, Theme.PrimaryDark))
        InvPaint.DrawText(g, ItemName, Theme.F(12.5F, True), New Rectangle(0, 14, Math.Max(40, Width - pw - 12), 30), Theme.TextDark)
        InvPaint.DrawText(g, "Restock to", Theme.F(10.5F, True), New Rectangle(0, 58, 100, 46), Theme.TextMuted)
    End Sub
End Class

''' <summary>Right-hand "Needs attention" card.</summary>
Public Class AttentionCard
    Inherits RoundedPanel

    Public ReadOnly Items As New List(Of AttentionItem)
    Private Const Pad As Integer = 24
    Private Const SubText As String = "Low or out of stock. Set a new number to restock."
    Private Const NoneText As String = "Nothing needs attention. Every tracked item is well stocked."
    Private _subH As Integer = 24

    Public Sub New()
        Radius = 20
        Shadow = True
    End Sub

    ''' <summary>Positions the items for the given card width and returns the card height.</summary>
    Public Function LayoutItems(w As Integer) As Integer
        Dim txt As String = If(Items.Count = 0, NoneText, SubText)
        _subH = TextRenderer.MeasureText(txt, Theme.F(10.5F), New Size(w - 2 * Pad, 1000), TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding).Height
        Dim y As Integer = 62 + _subH + 16
        For Each it As AttentionItem In Items
            it.SetBounds(Pad, y, w - 2 * Pad, AttentionItem.ItemH)
            y += AttentionItem.ItemH
        Next
        Return y + 22
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g As Graphics = e.Graphics
        TextRenderer.DrawText(g, "Needs attention", Theme.F(17, True), New Point(Pad, 20), Theme.TextDark, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, If(Items.Count = 0, NoneText, SubText), Theme.F(10.5F), New Rectangle(Pad, 62, Width - 2 * Pad, _subH + 4),
                              Theme.TextMuted, TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding)
    End Sub
End Class

''' <summary>One item that does not count stock: Available / Sold out switch and the "Track stock" button.</summary>
Public Class NtRow
    Inherits Control

    Public ReadOnly ProductId As Integer
    Public ReadOnly ItemName As String
    Public Event TrackClicked(row As NtRow)
    Public Event AvailableChanged(row As NtRow, value As Boolean)

    Private ReadOnly _category As String
    Private ReadOnly _img As New CoverImage()
    Private ReadOnly _toggle As New ToggleSwitch()
    Private ReadOnly _track As RoundedButton
    Private _isLast As Boolean

    Public Sub New(productId As Integer, name As String, category As String, available As Boolean, img As Image)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Me.ProductId = productId
        ItemName = name
        _category = category
        BackColor = Color.White

        _img.Radius = 12
        _img.Image = img
        _img.PlaceholderText = name
        _img.BackColor = Color.White
        _img.Size = New Size(62, 62)

        _toggle.BackColor = Color.White
        _toggle.Checked = available

        _track = Ui.OutlineBtn("Track stock", 132, 46)
        _track.Radius = 23
        _track.FillColor = Color.White
        _track.HoverColor = Theme.PrimarySoft
        _track.TextColor = Theme.TextDark
        _track.BorderColor = Theme.Border
        _track.Font = Theme.F(11, True)

        Controls.Add(_img)
        Controls.Add(_toggle)
        Controls.Add(_track)
        AddHandler _toggle.CheckedChanged, Sub(s, e)
                                               Invalidate()
                                               RaiseEvent AvailableChanged(Me, _toggle.Checked)
                                           End Sub
        AddHandler _track.Click, Sub(s, e) RaiseEvent TrackClicked(Me)
        PlaceChildren()
    End Sub

    Public Property IsLast As Boolean
        Get
            Return _isLast
        End Get
        Set(value As Boolean)
            _isLast = value
            Invalidate()
        End Set
    End Property

    Private Sub PlaceChildren()
        If _track Is Nothing Then Return
        _img.Location = New Point(16, (Height - 62) \ 2)
        _track.Location = New Point(Width - InvPaint.PadR - 132, (Height - 46) \ 2)
        _toggle.Location = New Point(_track.Left - 18 - 54, (Height - 30) \ 2)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        PlaceChildren()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        Dim stText As String = If(_toggle.Checked, "Available", "Sold out")
        Dim stX As Integer = _toggle.Left - 12 - 84
        InvPaint.DrawText(g, stText, Theme.F(10.5F, True), New Rectangle(stX, 0, 84, Height), If(_toggle.Checked, Theme.Success, Theme.Danger), TextFormatFlags.Right)
        Dim right As Integer = stX - 14
        InvPaint.DrawText(g, ItemName, Theme.F(12.5F, True), New Rectangle(96, Height \ 2 - 28, Math.Max(40, right - 96), 28), Theme.TextDark)
        InvPaint.DrawText(g, _category, Theme.F(10.5F), New Rectangle(96, Height \ 2, Math.Max(40, right - 96), 24), Theme.TextMuted)
        If Not _isLast Then
            Using pen As New Pen(Theme.Border, 1)
                g.DrawLine(pen, 16, Height - 1, Width - 16, Height - 1)
            End Using
        End If
    End Sub
End Class

''' <summary>"Not tracked" card: header with collapse button, hint text and the rows.</summary>
Public Class NotTrackedCard
    Inherits RoundedPanel

    Public Const HeaderH As Integer = 96
    Public ReadOnly Rows As New List(Of NtRow)
    Public Event ToggleRequested As EventHandler
    Private ReadOnly _collapse As New IconButton()
    Private _expanded As Boolean = True

    Public Sub New()
        Radius = 20
        Shadow = True
        InvPaint.SoftCircleBtn(_collapse, "minus")
        Controls.Add(_collapse)
        AddHandler _collapse.Click, Sub(s, e) RaiseEvent ToggleRequested(Me, EventArgs.Empty)
    End Sub

    Public Property Expanded As Boolean
        Get
            Return _expanded
        End Get
        Set(value As Boolean)
            _expanded = value
            _collapse.IconName = If(value, "minus", "plus")
            _collapse.Redraw()
            For Each r As NtRow In Rows
                r.Visible = value
            Next
            Invalidate()
        End Set
    End Property

    Public Function HeightFor(n As Integer) As Integer
        If Not _expanded Then Return 68
        Return HeaderH + n * InvPaint.RowH + 12
    End Function

    Public Sub LayoutRows()
        _collapse.Location = New Point(Width - 22 - 36, 16)
        Dim rw As Integer = Math.Max(100, Width - 20)
        For i As Integer = 0 To Rows.Count - 1
            Rows(i).SetBounds(10, HeaderH + i * InvPaint.RowH, rw, InvPaint.RowH)
            Rows(i).IsLast = (i = Rows.Count - 1)
            Rows(i).Visible = _expanded
        Next
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g As Graphics = e.Graphics
        Dim t As String = "Not tracked"
        Dim w As Integer = TextRenderer.MeasureText(t, Theme.F(14, True), New Size(400, 40), TextFormatFlags.NoPadding).Width
        TextRenderer.DrawText(g, t, Theme.F(14, True), New Point(26, 18), Theme.TextDark, TextFormatFlags.NoPadding)
        Dim n As String = " " & ChrW(&HB7) & " " & Rows.Count.ToString() & If(Rows.Count = 1, " item", " items")
        TextRenderer.DrawText(g, n, Theme.F(10.5F), New Point(26 + w, 23), Theme.TextMuted, TextFormatFlags.NoPadding)
        If _expanded Then
            TextRenderer.DrawText(g, "These stay Available or Sold out by hand. Track stock to count them down automatically.", Theme.F(10.5F),
                                  New Rectangle(26, 56, Width - 52, 28), Theme.TextMuted, TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
            Using pen As New Pen(Theme.Border, 1)
                g.DrawLine(pen, 26, HeaderH - 4, Width - 26, HeaderH - 4)
            End Using
        End If
    End Sub
End Class

''' <summary>All / Low / Out chips with counts (same look as the Menu chips, without the "+" chip).</summary>
Public Class InvFilterChips
    Inherits Control

    Public Event SelectedChanged As EventHandler
    Private ReadOnly _names As New List(Of String)
    Private ReadOnly _counts As New List(Of Integer)
    Private ReadOnly _rects As New List(Of Rectangle)
    Private ReadOnly _fName As Font = Theme.F(11.5F, True)
    Private ReadOnly _fCount As Font = Theme.F(9.5F)
    Private _sel As Integer

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Theme.Bg
    End Sub

    Public ReadOnly Property SelectedIndex As Integer
        Get
            Return _sel
        End Get
    End Property

    Public Sub SetItems(names As List(Of String), counts As List(Of Integer), selected As Integer)
        _names.Clear()
        _names.AddRange(names)
        _counts.Clear()
        _counts.AddRange(counts)
        _sel = Math.Max(0, Math.Min(selected, _names.Count - 1))
        Invalidate()
    End Sub

    Private Shared Function Measure(t As String, f As Font) As Integer
        Return TextRenderer.MeasureText(t, f, New Size(1000, 50), TextFormatFlags.NoPadding).Width
    End Function

    Private Sub Calc()
        _rects.Clear()
        Dim x As Integer = 0
        For i As Integer = 0 To _names.Count - 1
            Dim w As Integer = Measure(_names(i), _fName) + Measure(_counts(i).ToString(), _fCount) + 44
            _rects.Add(New Rectangle(x, 0, w, 44))
            x += w + 10
        Next
    End Sub

    Public Function RequiredWidth() As Integer
        Calc()
        If _rects.Count = 0 Then Return 10
        Return _rects(_rects.Count - 1).Right
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Calc()
        For i As Integer = 0 To _rects.Count - 1
            Dim r As Rectangle = _rects(i)
            Dim isSel As Boolean = (i = _sel)
            Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), 22), b As New SolidBrush(If(isSel, Theme.TextDark, Color.White)), pen As New Pen(Theme.Border, 1.0F)
                g.FillPath(b, p)
                If Not isSel Then g.DrawPath(pen, p)
            End Using
            Dim nw As Integer = Measure(_names(i), _fName)
            TextRenderer.DrawText(g, _names(i), _fName, New Rectangle(r.X + 20, r.Y, nw + 4, r.Height), If(isSel, Color.White, Theme.TextMuted),
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            TextRenderer.DrawText(g, _counts(i).ToString(), _fCount, New Rectangle(r.X + 20 + nw + 6, r.Y + 2, 60, r.Height), If(isSel, Color.FromArgb(200, 194, 186), Theme.TextMuted),
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        Next
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim over As Boolean = False
        For Each r As Rectangle In _rects
            If r.Contains(e.Location) Then over = True
        Next
        Cursor = If(over, Cursors.Hand, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button <> MouseButtons.Left Then Return
        For i As Integer = 0 To _rects.Count - 1
            If _rects(i).Contains(e.Location) Then
                If i <> _sel Then
                    _sel = i
                    Invalidate()
                    RaiseEvent SelectedChanged(Me, EventArgs.Empty)
                End If
                Return
            End If
        Next
    End Sub
End Class

''' <summary>"5 tracked · 1 low · 0 out", right aligned, numbers in bold.</summary>
Public Class InvSummary
    Inherits Control

    Private _tracked As Integer
    Private _low As Integer
    Private _out As Integer

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Theme.Bg
    End Sub

    Public Sub SetCounts(tracked As Integer, lowN As Integer, outN As Integer)
        _tracked = tracked
        _low = lowN
        _out = outN
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        Dim fb As Font = Theme.F(12, True)
        Dim fn As Font = Theme.F(12)
        Dim dot As String = " " & ChrW(&HB7) & " "
        Dim parts As New List(Of KeyValuePair(Of String, Boolean))
        parts.Add(New KeyValuePair(Of String, Boolean)(_tracked.ToString(), True))
        parts.Add(New KeyValuePair(Of String, Boolean)(" tracked" & dot, False))
        parts.Add(New KeyValuePair(Of String, Boolean)(_low.ToString(), True))
        parts.Add(New KeyValuePair(Of String, Boolean)(" low" & dot, False))
        parts.Add(New KeyValuePair(Of String, Boolean)(_out.ToString(), True))
        parts.Add(New KeyValuePair(Of String, Boolean)(" out", False))
        Dim total As Integer = 0
        For Each p As KeyValuePair(Of String, Boolean) In parts
            total += TextRenderer.MeasureText(p.Key, If(p.Value, fb, fn), New Size(1000, 40), TextFormatFlags.NoPadding).Width
        Next
        Dim x As Integer = Width - total
        For Each p As KeyValuePair(Of String, Boolean) In parts
            Dim f As Font = If(p.Value, fb, fn)
            Dim w As Integer = TextRenderer.MeasureText(p.Key, f, New Size(1000, 40), TextFormatFlags.NoPadding).Width
            TextRenderer.DrawText(g, p.Key, f, New Rectangle(x, 0, w + 2, Height), If(p.Value, Theme.TextDark, Theme.TextMuted),
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            x += w
        Next
    End Sub
End Class

''' <summary>Popup asked when "Track stock" is clicked: starting quantity and the Low alert level.</summary>
Public Class TrackStockForm
    Inherits Form

    Public Qty As Integer
    Public LowAt As Integer = 10
    Private ReadOnly _qty As New AppTextBox()
    Private ReadOnly _low As New AppTextBox()

    Public Sub New(itemName As String)
        Text = "Track stock"
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        BackColor = Color.White
        Font = Theme.F(10)

        Dim fullW As Integer = 440
        Dim x As Integer = 28
        Dim cw As Integer = fullW - 56
        Dim y As Integer = 26

        Dim t As Label = Ui.Lbl("Track stock", 16, True)
        t.Location = New Point(x, y)
        Controls.Add(t)
        y += 38
        Dim sub1 As Label = Ui.WrapLbl(itemName & " will count down with every order. At 0 it becomes unavailable to customers.", 10, cw, Theme.TextMuted)
        sub1.Location = New Point(x, y)
        Controls.Add(sub1)
        y += sub1.Height + 18

        Dim c1 As Label = Ui.Lbl("Starting quantity", 10.5F, True)
        c1.Location = New Point(x, y)
        Controls.Add(c1)
        y += 26
        _qty.Font = Theme.F(11)
        _qty.PlaceholderText = "e.g. 20"
        _qty.MaxLength = 5
        MenuFmt.NumericOnly(_qty, False)
        _qty.SetBounds(x, y, cw, 48)
        AddHandler _qty.EnterPressed, AddressOf OnOk
        Controls.Add(_qty)
        y += 48 + 16

        Dim c2 As Label = Ui.Lbl("Show Low when stock is at or below", 10.5F, True)
        c2.Location = New Point(x, y)
        Controls.Add(c2)
        y += 26
        _low.Font = Theme.F(11)
        _low.PlaceholderText = "10"
        _low.MaxLength = 5
        MenuFmt.NumericOnly(_low, False)
        _low.Text = "10"
        _low.SetBounds(x, y, cw, 48)
        AddHandler _low.EnterPressed, AddressOf OnOk
        Controls.Add(_low)
        y += 48 + 24

        Dim cancel As RoundedButton = Ui.OutlineBtn("Cancel", 120, 46)
        cancel.Location = New Point(x, y)
        AddHandler cancel.Click, Sub(s, e) DialogResult = DialogResult.Cancel
        Controls.Add(cancel)
        Dim ok As RoundedButton = Ui.PrimaryBtn("Start tracking", 170, 46)
        ok.Location = New Point(x + cw - 170, y)
        AddHandler ok.Click, AddressOf OnOk
        Controls.Add(ok)

        ClientSize = New Size(fullW, y + 46 + 26)
    End Sub

    Private Sub OnOk(sender As Object, e As EventArgs)
        Dim q As Integer
        Dim l As Integer
        If Not MenuFmt.TryInt(_qty.Text, q, 0, 99999) Then
            Ui.Warn("Enter the starting quantity (0 or more).")
            _qty.FocusInput()
            Return
        End If
        If Not MenuFmt.TryInt(_low.Text, l, 0, 99999) Then
            Ui.Warn("Enter the number at which the item shows as Low (0 or more).")
            _low.FocusInput()
            Return
        End If
        Qty = q
        LowAt = l
        DialogResult = DialogResult.OK
    End Sub
End Class

''' <summary>The Inventory tab of the seller window.</summary>
Public Class InventoryPage
    Inherits PageBase

    Private ReadOnly _stallId As Integer
    Private ReadOnly _bar As New Panel()
    Private ReadOnly _title As Label
    Private ReadOnly _summary As New InvSummary()
    Private ReadOnly _chips As New InvFilterChips()
    Private ReadOnly _search As New AppTextBox()
    Private ReadOnly _table As New InvTableCard()
    Private ReadOnly _attn As New AttentionCard()
    Private ReadOnly _nt As New NotTrackedCard()
    Private ReadOnly _debounce As New Timer()
    Private ReadOnly _images As New Dictionary(Of String, Image)
    Private _data As DataTable
    Private _trackedTotal As Integer
    Private _ntExpanded As Boolean = True

    Public Sub New(stallId As Integer)
        _stallId = stallId
        _bar.BackColor = Theme.Primary
        _title = Ui.Lbl("Inventory", 26, True)
        _search.LeftIcon = "search"
        _search.PlaceholderText = "Search items"
        _search.Font = Theme.F(11)
        _search.MaxLength = 40

        Controls.Add(_bar)
        Controls.Add(_title)
        Controls.Add(_summary)
        Controls.Add(_chips)
        Controls.Add(_search)
        Controls.Add(_table)
        Controls.Add(_attn)
        Controls.Add(_nt)

        _debounce.Interval = 250
        AddHandler _debounce.Tick, Sub(s, e)
                                       _debounce.Stop()
                                       BuildRows()
                                   End Sub
        AddHandler _search.TextChanged, Sub(s, e)
                                            _debounce.Stop()
                                            _debounce.Start()
                                        End Sub
        AddHandler _chips.SelectedChanged, Sub(s, e) BuildRows()
        AddHandler _nt.ToggleRequested, AddressOf OnToggleNotTracked

        Try
            Reload()
        Catch ex As Exception
            Ui.Warn("The inventory needs a database update. Run patch_inventory.sql once in MySQL, then restart the app." & vbCrLf & vbCrLf & ex.Message)
        End Try
    End Sub

    ' ---------------------------------------------------------------- data
    Private Function GetImg(path As String) As Image
        If String.IsNullOrEmpty(path) Then Return Nothing
        If Not _images.ContainsKey(path) Then _images(path) = Ui.LoadImage(path)
        Return _images(path)
    End Function

    ''' <summary>Re-read the stall's items from MySQL and rebuild the page.</summary>
    Public Sub Reload()
        _data = SellerSvc.GetInventory(_stallId)
        Dim low As Integer = 0
        Dim outN As Integer = 0
        _trackedTotal = 0
        For Each r As DataRow In _data.Rows
            If Not Db.GetBool(r, "track_stock") Then Continue For
            _trackedTotal += 1
            Dim k As Integer = InvPaint.StatusOf(Db.GetInt(r, "stock"), Db.GetInt(r, "low_stock_at"))
            If k = 1 Then low += 1
            If k = 2 Then outN += 1
        Next
        _summary.SetCounts(_trackedTotal, low, outN)
        _chips.SetItems(New List(Of String)(New String() {"All", "Low", "Out"}),
                        New List(Of Integer)(New Integer() {_trackedTotal, low, outN}), _chips.SelectedIndex)
        BuildRows()
    End Sub

    Private Sub ClearControls()
        For Each r As InvRow In _table.Rows
            _table.Controls.Remove(r)
            r.Dispose()
        Next
        _table.Rows.Clear()
        For Each a As AttentionItem In _attn.Items
            _attn.Controls.Remove(a)
            a.Dispose()
        Next
        _attn.Items.Clear()
        For Each n As NtRow In _nt.Rows
            _nt.Controls.Remove(n)
            n.Dispose()
        Next
        _nt.Rows.Clear()
    End Sub

    Private Sub BuildRows()
        If _data Is Nothing Then Return
        ClearControls()
        Dim q As String = _search.Text.Trim()
        Dim filter As Integer = _chips.SelectedIndex
        Dim attention As New List(Of DataRow)
        For Each r As DataRow In _data.Rows
            Dim id As Integer = Db.GetInt(r, "id")
            Dim nm As String = Db.GetStr(r, "name")
            Dim cat As String = Db.GetStr(r, "category")
            Dim stock As Integer = Db.GetInt(r, "stock")
            Dim lowAt As Integer = Db.GetInt(r, "low_stock_at")
            Dim tracked As Boolean = Db.GetBool(r, "track_stock")
            Dim matches As Boolean = (q = "" OrElse nm.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            If tracked Then
                Dim kind As Integer = InvPaint.StatusOf(stock, lowAt)
                If kind > 0 Then attention.Add(r)
                If Not matches Then Continue For
                If filter = 1 AndAlso kind <> 1 Then Continue For
                If filter = 2 AndAlso kind <> 2 Then Continue For
                Dim row As New InvRow(id, nm, cat, stock, lowAt, GetImg(Db.GetStr(r, "image_path")))
                AddHandler row.Adjusted, AddressOf OnRowAdjusted
                AddHandler row.TypedStock, AddressOf OnRowTyped
                _table.Controls.Add(row)
                _table.Rows.Add(row)
            Else
                If filter <> 0 OrElse Not matches Then Continue For
                Dim nr As New NtRow(id, nm, cat, Db.GetBool(r, "is_available"), GetImg(Db.GetStr(r, "image_path")))
                AddHandler nr.TrackClicked, AddressOf OnTrackClicked
                AddHandler nr.AvailableChanged, AddressOf OnAvailableChanged
                _nt.Controls.Add(nr)
                _nt.Rows.Add(nr)
            End If
        Next

        ' out of stock first, then the lowest numbers
        attention.Sort(Function(a, b) Db.GetInt(a, "stock").CompareTo(Db.GetInt(b, "stock")))
        For Each r As DataRow In attention
            Dim it As New AttentionItem(Db.GetInt(r, "id"), Db.GetStr(r, "name"), Db.GetInt(r, "stock"))
            AddHandler it.SetClicked, AddressOf OnRestock
            _attn.Controls.Add(it)
            _attn.Items.Add(it)
        Next

        If _trackedTotal = 0 Then
            _table.EmptyText = "No tracked items yet. Use " & ChrW(&H201C) & "Track stock" & ChrW(&H201D) & " below to start counting an item."
        Else
            _table.EmptyText = "No items match."
        End If
        _nt.Expanded = _ntExpanded
        RelayoutKeepScroll()
    End Sub

    ' ---------------------------------------------------------------- actions
    ''' <summary>Runs a database change, then reloads after the current click has finished (the controls that raised it get replaced).</summary>
    Private Sub Act(work As Action)
        Try
            work()
        Catch ex As Exception
            Toast.Show(Me, "Could not update the stock: " & ex.Message, True)
        End Try
        If IsHandleCreated AndAlso Not IsDisposed Then BeginInvoke(New Action(AddressOf SafeReload))
    End Sub

    Private Sub SafeReload()
        If IsDisposed Then Return
        Try
            Reload()
        Catch ex As Exception
            Toast.Show(Me, "Could not refresh the inventory: " & ex.Message, True)
        End Try
    End Sub

    Private Sub OnRowAdjusted(row As InvRow, delta As Integer)
        If row.Stock + delta < 0 Then Return
        Dim id As Integer = row.ProductId
        Act(Sub() SellerSvc.AdjustStock(id, _stallId, delta))
    End Sub

    Private Sub OnRowTyped(row As InvRow, value As Integer)
        Dim id As Integer = row.ProductId
        Act(Sub() SellerSvc.SetStock(id, _stallId, value))
    End Sub

    Private Sub OnRestock(item As AttentionItem, qty As Integer)
        Dim id As Integer = item.ProductId
        Dim nm As String = item.ItemName
        Act(Sub() SellerSvc.SetStock(id, _stallId, qty))
        Toast.Show(Me, nm & " restocked to " & qty.ToString())
    End Sub

    Private Sub OnTrackClicked(row As NtRow)
        Dim id As Integer = row.ProductId
        Dim nm As String = row.ItemName
        Using f As New TrackStockForm(nm)
            If f.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            Dim qty As Integer = f.Qty
            Dim lo As Integer = f.LowAt
            Act(Sub() SellerSvc.StartTracking(id, _stallId, qty, lo))
            Toast.Show(Me, nm & " is now tracked")
        End Using
    End Sub

    Private Sub OnAvailableChanged(row As NtRow, value As Boolean)
        Dim id As Integer = row.ProductId
        Act(Sub() SellerSvc.SetAvailable(id, _stallId, value))
    End Sub

    Private Sub OnToggleNotTracked(sender As Object, e As EventArgs)
        _ntExpanded = Not _ntExpanded
        _nt.Expanded = _ntExpanded
        RelayoutKeepScroll()
    End Sub

    ' ---------------------------------------------------------------- layout
    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim w As Integer = contentWidth
        _bar.SetBounds(M, 10, 46, 5)
        _title.Location = New Point(M, 22)
        _summary.SetBounds(M + w - 340, 38, 340, 30)

        Dim wide As Boolean = (w >= 900)
        Dim gap As Integer = 24
        Dim rightW As Integer = If(wide, 340, 0)
        Dim leftW As Integer = If(wide, w - rightW - gap, w)
        Dim top As Integer = 90

        Dim chipsW As Integer = _chips.RequiredWidth()
        _chips.SetBounds(M, top, chipsW, 44)
        Dim sw As Integer = Math.Min(260, Math.Max(140, leftW - chipsW - 16))
        _search.SetBounds(M + leftW - sw, top - 2, sw, 48)
        Dim y As Integer = top + 48 + 22
        Dim rightBottom As Integer = 0

        If wide Then
            Dim ah As Integer = _attn.LayoutItems(rightW)
            _attn.SetBounds(M + leftW + gap, top, rightW, ah)
            _attn.Visible = True
            rightBottom = top + ah
        ElseIf _attn.Items.Count > 0 Then
            Dim ah As Integer = _attn.LayoutItems(w)
            _attn.SetBounds(M, y, w, ah)
            _attn.Visible = True
            y += ah + 20
        Else
            _attn.Visible = False
        End If

        Dim th As Integer = _table.HeightFor(_table.Rows.Count)
        _table.SetBounds(M, y, leftW, th)
        _table.LayoutRows()
        y += th + 20

        If _nt.Rows.Count > 0 Then
            _nt.Visible = True
            Dim nh As Integer = _nt.HeightFor(_nt.Rows.Count)
            _nt.SetBounds(M, y, leftW, nh)
            _nt.LayoutRows()
            y += nh + 20
        Else
            _nt.Visible = False
        End If

        SetContentHeight(Math.Max(y, rightBottom + 20) + 8)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _debounce.Stop()
            _debounce.Dispose()
            For Each kv As KeyValuePair(Of String, Image) In _images
                If kv.Value IsNot Nothing Then kv.Value.Dispose()
            Next
            _images.Clear()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
