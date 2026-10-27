Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Rounded text box with optional left icon and a show/hide eye for passwords.</summary>
Public Class AppTextBox
    Inherits Control

    Private ReadOnly _tb As New TextBox()
    Private ReadOnly _ph As New Label()
    Private _isPassword As Boolean
    Private _reveal As Boolean
    Private _focus As Boolean
    Private _state As Integer
    Private _leftIcon As String = ""
    Public Event EnterPressed As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(300, 44)
        Font = Theme.F(10.5F)
        _tb.BorderStyle = BorderStyle.None
        _tb.Font = Font
        _tb.BackColor = Theme.FieldBg
        _tb.ForeColor = Theme.TextDark
        _ph.AutoSize = False
        _ph.Font = Font
        _ph.ForeColor = Theme.TextMuted
        _ph.BackColor = Theme.FieldBg
        _ph.Cursor = Cursors.IBeam
        Controls.Add(_tb)
        Controls.Add(_ph)
        _ph.BringToFront()
        AddHandler _tb.TextChanged, AddressOf InnerTextChanged
        AddHandler _tb.Enter, AddressOf InnerEnter
        AddHandler _tb.Leave, AddressOf InnerLeave
        AddHandler _tb.KeyDown, AddressOf InnerKeyDown
        AddHandler _ph.Click, AddressOf PlaceholderClick
        LayoutInner()
    End Sub

    Public ReadOnly Property Inner As TextBox
        Get
            Return _tb
        End Get
    End Property

    Public Overrides Property Text As String
        Get
            Return _tb.Text
        End Get
        Set(value As String)
            _tb.Text = value
        End Set
    End Property

    Public Property PlaceholderText As String
        Get
            Return _ph.Text
        End Get
        Set(value As String)
            _ph.Text = value
            UpdatePlaceholder()
        End Set
    End Property

    Public Property MaxLength As Integer
        Get
            Return _tb.MaxLength
        End Get
        Set(value As Integer)
            _tb.MaxLength = value
        End Set
    End Property

    Public Property LeftIcon As String
        Get
            Return _leftIcon
        End Get
        Set(value As String)
            _leftIcon = value
            LayoutInner()
            Invalidate()
        End Set
    End Property

    Public Property IsPassword As Boolean
        Get
            Return _isPassword
        End Get
        Set(value As Boolean)
            _isPassword = value
            _tb.UseSystemPasswordChar = value AndAlso Not _reveal
            LayoutInner()
            Invalidate()
        End Set
    End Property

    ''' <summary>0 = neutral, 1 = valid (green), 2 = invalid (red)</summary>
    Public Property State As Integer
        Get
            Return _state
        End Get
        Set(value As Integer)
            _state = value
            Invalidate()
        End Set
    End Property

    Public Sub FocusInput()
        _tb.Focus()
    End Sub

    Private Sub UpdatePlaceholder()
        _ph.Visible = (_tb.Text.Length = 0)
    End Sub

    Private Sub InnerTextChanged(sender As Object, e As EventArgs)
        UpdatePlaceholder()
        MyBase.OnTextChanged(EventArgs.Empty)
    End Sub

    Private Sub InnerEnter(sender As Object, e As EventArgs)
        _focus = True
        Invalidate()
    End Sub

    Private Sub InnerLeave(sender As Object, e As EventArgs)
        _focus = False
        Invalidate()
    End Sub

    Private Sub InnerKeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            e.Handled = True
            RaiseEvent EnterPressed(Me, EventArgs.Empty)
        End If
    End Sub

    Private Sub PlaceholderClick(sender As Object, e As EventArgs)
        _tb.Focus()
    End Sub

    Private Function RightPad() As Integer
        Return 14 + If(_isPassword, 30, 0)
    End Function

    Private Sub LayoutInner()
        Dim lp As Integer = 14 + If(_leftIcon <> "", 26, 0)
        Dim w As Integer = Math.Max(20, Width - lp - RightPad())
        _tb.Font = Font
        _ph.Font = Font
        _tb.Width = w
        _tb.Left = lp
        _tb.Top = Math.Max(0, CInt((Height - _tb.Height) / 2))
        _ph.SetBounds(_tb.Left + 1, _tb.Top, w, _tb.Height)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        LayoutInner()
    End Sub

    Protected Overrides Sub OnFontChanged(e As EventArgs)
        MyBase.OnFontChanged(e)
        LayoutInner()
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        If _isPassword AndAlso e.X > Width - RightPad() Then
            _reveal = Not _reveal
            _tb.UseSystemPasswordChar = Not _reveal
            Invalidate()
        Else
            _tb.Focus()
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _isPassword AndAlso e.X > Width - RightPad() Then
            Cursor = Cursors.Hand
        Else
            Cursor = Cursors.IBeam
        End If
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim bc As Color = Theme.Border
        Dim bw As Single = 1.5F
        If _state = 2 Then
            bc = Theme.Danger
        ElseIf _focus Then
            bc = Theme.Primary
            bw = 2.0F
        ElseIf _state = 1 Then
            bc = Theme.Success
        End If
        Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(1, 1, Width - 3, Height - 3), 11), br As New SolidBrush(Theme.FieldBg), pen As New Pen(bc, bw)
            g.FillPath(br, path)
            g.DrawPath(pen, path)
        End Using
        If _leftIcon <> "" Then
            Icons.Draw(g, _leftIcon, New Rectangle(14, CInt((Height - 17) / 2), 17, 17), Theme.TextMuted)
        End If
        If _isPassword Then
            Icons.Draw(g, If(_reveal, "eyeoff", "eye"), New Rectangle(Width - 14 - 20, CInt((Height - 20) / 2), 20, 20), If(_reveal, Theme.Primary, Theme.TextMuted))
        End If
    End Sub
End Class

''' <summary>One inline validation rule (shown under a field as a green/red chip).</summary>
Public Class Rule
    Public Text As String
    Public Check As Func(Of String, Boolean)

    Public Sub New(text As String, check As Func(Of String, Boolean))
        Me.Text = text
        Me.Check = check
    End Sub
End Class

''' <summary>Caption + AppTextBox + live validation rules line.</summary>
Public Class ValidatedField
    Inherits Control

    Private ReadOnly _caption As New Label()
    Public ReadOnly Box As New AppTextBox()
    Private ReadOnly _rules As New List(Of Rule)
    Private _results As New List(Of Boolean)
    Private _touched As Boolean
    Public Event ValueChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        _caption.AutoSize = False
        _caption.Font = Theme.F(9.5F, True)
        _caption.ForeColor = Theme.TextDark
        Controls.Add(_caption)
        Controls.Add(Box)
        AddHandler Box.TextChanged, AddressOf BoxTextChanged
        Size = New Size(380, 68)
        DoLayoutFields()
    End Sub

    Public Property Caption As String
        Get
            Return _caption.Text
        End Get
        Set(value As String)
            _caption.Text = value
        End Set
    End Property

    Public Property IsPassword As Boolean
        Get
            Return Box.IsPassword
        End Get
        Set(value As Boolean)
            Box.IsPassword = value
        End Set
    End Property

    Public ReadOnly Property Value As String
        Get
            Return Box.Text
        End Get
    End Property

    Public Sub AddRule(text As String, check As Func(Of String, Boolean))
        _rules.Add(New Rule(text, check))
        _results.Add(False)
        Height = 68 + 24
        DoLayoutFields()
    End Sub

    Public Sub ClearValue()
        _touched = False
        Box.Text = ""
        Evaluate()
    End Sub

    Private Sub DoLayoutFields()
        _caption.SetBounds(0, 0, Width, 20)
        Box.SetBounds(0, 22, Width, 44)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        DoLayoutFields()
    End Sub

    Private Sub BoxTextChanged(sender As Object, e As EventArgs)
        Evaluate()
        RaiseEvent ValueChanged(Me, EventArgs.Empty)
    End Sub

    ''' <summary>Re-run the rules (call when something the rules depend on changed, e.g. the password above a confirm field).</summary>
    Public Sub Evaluate()
        _results.Clear()
        Dim allOk As Boolean = True
        For Each r As Rule In _rules
            Dim ok As Boolean = True
            Try
                ok = r.Check(Box.Text)
            Catch
                ok = True
            End Try
            _results.Add(ok)
            If Not ok Then allOk = False
        Next
        If _rules.Count = 0 Then
            Box.State = 0
        ElseIf Box.Text.Length = 0 AndAlso Not _touched Then
            Box.State = 0
        ElseIf allOk Then
            Box.State = 1
        Else
            Box.State = 2
        End If
        Invalidate()
    End Sub

    ''' <summary>Marks the field as touched (empty counts as an error) and returns True when every rule passes.</summary>
    Public Function IsValid() As Boolean
        _touched = True
        Evaluate()
        For Each ok As Boolean In _results
            If Not ok Then Return False
        Next
        Return True
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim x As Integer = 2
        Dim y As Integer = 72
        Dim show As Boolean = (Box.Text.Length > 0 OrElse _touched)
        For i As Integer = 0 To _rules.Count - 1
            Dim ok As Boolean = (i < _results.Count AndAlso _results(i))
            Dim col As Color = Color.FromArgb(160, 154, 146)
            Dim icon As String = ""
            If show Then
                If ok Then
                    col = Theme.Success
                    icon = "check"
                Else
                    col = Theme.Danger
                    icon = "x"
                End If
            End If
            Dim fnt As Font = Theme.F(8.5F)
            Dim tw As Integer = TextRenderer.MeasureText(_rules(i).Text, fnt, New Size(1000, 40), TextFormatFlags.NoPadding).Width
            If icon <> "" Then
                Icons.Draw(g, icon, New Rectangle(x, y + 3, 11, 11), col)
            Else
                Using br As New SolidBrush(col)
                    g.FillEllipse(br, x + 3, y + 6, 5, 5)
                End Using
            End If
            TextRenderer.DrawText(g, _rules(i).Text, fnt, New Rectangle(x + 15, y, tw + 4, 18), col, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            x += 15 + tw + 14
        Next
    End Sub
End Class

''' <summary>Horizontally draggable row of filter chips (All, Chicken, Rice Meals...).</summary>
Public Class ChipBar
    Inherits Control

    Private ReadOnly _items As New List(Of String)
    Private ReadOnly _rects As New List(Of Rectangle)
    Private _selected As Integer = 0
    Private _offset As Integer = 0
    Private _total As Integer = 0
    Private _downX As Integer
    Private _downOffset As Integer
    Private _dragging As Boolean
    Private _moved As Boolean
    Public Event SelectedChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(600, 44)
        Font = Theme.F(10, True)
        Cursor = Cursors.Hand
    End Sub

    Public ReadOnly Property SelectedIndex As Integer
        Get
            Return _selected
        End Get
    End Property

    Public ReadOnly Property SelectedText As String
        Get
            If _selected >= 0 AndAlso _selected < _items.Count Then Return _items(_selected)
            Return ""
        End Get
    End Property

    Public Sub SetItems(items As IEnumerable(Of String), Optional selected As Integer = 0)
        _items.Clear()
        _items.AddRange(items)
        _selected = selected
        _offset = 0
        Relayout()
        Invalidate()
    End Sub

    Private Sub Relayout()
        _rects.Clear()
        Dim x As Integer = 0
        For Each s As String In _items
            Dim tw As Integer = TextRenderer.MeasureText(s, Font, New Size(2000, 100), TextFormatFlags.NoPadding).Width
            Dim w As Integer = tw + 34
            _rects.Add(New Rectangle(x, 2, w, Height - 6))
            x += w + 10
        Next
        _total = Math.Max(0, x - 10)
        ClampOffset()
    End Sub

    Private Sub ClampOffset()
        Dim maxOff As Integer = Math.Max(0, _total - Width)
        If _offset > maxOff Then _offset = maxOff
        If _offset < 0 Then _offset = 0
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        Relayout()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        _dragging = True
        _moved = False
        _downX = e.X
        _downOffset = _offset
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _dragging Then
            Dim dx As Integer = e.X - _downX
            If Math.Abs(dx) > 5 Then _moved = True
            If _moved Then
                _offset = _downOffset - dx
                ClampOffset()
                Invalidate()
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _dragging AndAlso Not _moved Then
            For i As Integer = 0 To _rects.Count - 1
                Dim r As Rectangle = _rects(i)
                r.Offset(-_offset, 0)
                If r.Contains(e.Location) Then
                    If i <> _selected Then
                        _selected = i
                        Invalidate()
                        RaiseEvent SelectedChanged(Me, EventArgs.Empty)
                    End If
                    Exit For
                End If
            Next
        End If
        _dragging = False
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        For i As Integer = 0 To _rects.Count - 1
            Dim r As Rectangle = _rects(i)
            r.Offset(-_offset, 0)
            If r.Right < 0 OrElse r.Left > Width Then Continue For
            Dim sel As Boolean = (i = _selected)
            Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), r.Height \ 2)
                Using br As New SolidBrush(If(sel, Theme.Primary, Color.White))
                    g.FillPath(br, path)
                End Using
                If Not sel Then
                    Using pen As New Pen(Theme.Border, 1.5F)
                        g.DrawPath(pen, path)
                    End Using
                End If
            End Using
            TextRenderer.DrawText(g, _items(i), Font, r, If(sel, Color.White, Theme.TextDark), TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        Next
    End Sub
End Class

''' <summary>Row/grid of selectable option cards (sizes, dine in/take out, extras).</summary>
Public Class OptionButtons
    Inherits Control

    Private ReadOnly _titles As New List(Of String)
    Private ReadOnly _subs As New List(Of String)
    Private ReadOnly _selected As New HashSet(Of Integer)
    Public Property MultiSelect As Boolean = False
    Public Property ItemWidth As Integer = 130
    Public Property ItemHeight As Integer = 56
    Public Property Gap As Integer = 10
    Public Event SelectionChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(400, 56)
        Cursor = Cursors.Hand
    End Sub

    Public Sub SetItems(titles As IEnumerable(Of String), subs As IEnumerable(Of String), Optional selectedIndex As Integer = -1)
        _titles.Clear()
        _subs.Clear()
        _selected.Clear()
        _titles.AddRange(titles)
        _subs.AddRange(subs)
        If selectedIndex >= 0 Then _selected.Add(selectedIndex)
        Invalidate()
    End Sub

    Public ReadOnly Property SelectedIndex As Integer
        Get
            If _selected.Count = 0 Then Return -1
            Return _selected.Min()
        End Get
    End Property

    Public ReadOnly Property SelectedIndexes As List(Of Integer)
        Get
            Dim l As New List(Of Integer)(_selected)
            l.Sort()
            Return l
        End Get
    End Property

    Public Function RequiredHeight(forWidth As Integer) As Integer
        If _titles.Count = 0 Then Return ItemHeight
        Dim perRow As Integer = Math.Max(1, (forWidth + Gap) \ (ItemWidth + Gap))
        Dim rows As Integer = CInt(Math.Ceiling(_titles.Count / CDbl(perRow)))
        Return rows * ItemHeight + (rows - 1) * Gap
    End Function

    Private Function ItemRect(i As Integer) As Rectangle
        Dim perRow As Integer = Math.Max(1, (Width + Gap) \ (ItemWidth + Gap))
        Dim row As Integer = i \ perRow
        Dim col As Integer = i Mod perRow
        Return New Rectangle(col * (ItemWidth + Gap), row * (ItemHeight + Gap), ItemWidth, ItemHeight)
    End Function

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        For i As Integer = 0 To _titles.Count - 1
            If ItemRect(i).Contains(e.Location) Then
                If MultiSelect Then
                    If _selected.Contains(i) Then _selected.Remove(i) Else _selected.Add(i)
                Else
                    _selected.Clear()
                    _selected.Add(i)
                End If
                Invalidate()
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
                Exit For
            End If
        Next
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        For i As Integer = 0 To _titles.Count - 1
            Dim r As Rectangle = ItemRect(i)
            Dim sel As Boolean = _selected.Contains(i)
            Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3), 12)
                Using br As New SolidBrush(If(sel, Theme.PrimarySoft, Color.White))
                    g.FillPath(br, path)
                End Using
                Using pen As New Pen(If(sel, Theme.Primary, Theme.Border), If(sel, 2.0F, 1.5F))
                    g.DrawPath(pen, path)
                End Using
            End Using
            Dim col As Color = If(sel, Theme.Primary, Theme.TextDark)
            If _subs(i) <> "" Then
                TextRenderer.DrawText(g, _titles(i), Theme.F(10, True), New Rectangle(r.X + 8, r.Y + 6, r.Width - 16, r.Height \ 2 - 4), col, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
                TextRenderer.DrawText(g, _subs(i), Theme.F(9), New Rectangle(r.X + 8, r.Y + r.Height \ 2, r.Width - 16, r.Height \ 2 - 6), If(sel, Theme.Primary, Theme.TextMuted), TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Else
                TextRenderer.DrawText(g, _titles(i), Theme.F(10, True), r, col, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
            End If
        Next
    End Sub
End Class

''' <summary>Pill-shaped tab switcher with optional count badges (Active (2) | History).</summary>
Public Class SegmentTabs
    Inherits Control

    Private ReadOnly _items As New List(Of String)
    Private ReadOnly _badges As New List(Of Integer)
    Private _selected As Integer = 0
    Public Event SelectedChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(280, 46)
        Font = Theme.F(10, True)
        Cursor = Cursors.Hand
    End Sub

    Public Sub SetItems(items As IEnumerable(Of String), badges As IEnumerable(Of Integer))
        _items.Clear()
        _badges.Clear()
        _items.AddRange(items)
        _badges.AddRange(badges)
        Invalidate()
    End Sub

    Public Sub SetBadge(index As Integer, value As Integer)
        If index >= 0 AndAlso index < _badges.Count Then
            _badges(index) = value
            Invalidate()
        End If
    End Sub

    Public Property SelectedIndex As Integer
        Get
            Return _selected
        End Get
        Set(value As Integer)
            _selected = value
            Invalidate()
        End Set
    End Property

    Private Function SegRect(i As Integer) As Rectangle
        Dim segW As Integer = CInt((Width - 8) / Math.Max(1, _items.Count))
        Return New Rectangle(4 + i * segW, 4, segW, Height - 8)
    End Function

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        For i As Integer = 0 To _items.Count - 1
            If SegRect(i).Contains(e.Location) AndAlso i <> _selected Then
                _selected = i
                Invalidate()
                RaiseEvent SelectedChanged(Me, EventArgs.Empty)
                Exit For
            End If
        Next
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), Height \ 2), br As New SolidBrush(Color.White), pen As New Pen(Theme.Border, 1.5F)
            g.FillPath(br, path)
            g.DrawPath(pen, path)
        End Using
        For i As Integer = 0 To _items.Count - 1
            Dim r As Rectangle = SegRect(i)
            Dim sel As Boolean = (i = _selected)
            If sel Then
                Using path As GraphicsPath = Theme.RoundRectPath(r, r.Height \ 2), br As New SolidBrush(Theme.Primary)
                    g.FillPath(br, path)
                End Using
            End If
            Dim tw As Integer = TextRenderer.MeasureText(_items(i), Font, New Size(1000, 50), TextFormatFlags.NoPadding).Width
            Dim hasBadge As Boolean = (_badges(i) > 0)
            Dim bw As Integer = If(hasBadge, 26, 0)
            Dim startX As Integer = r.X + CInt((r.Width - tw - bw) / 2)
            TextRenderer.DrawText(g, _items(i), Font, New Rectangle(startX, r.Y, tw + 2, r.Height), If(sel, Color.White, Theme.TextDark), TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            If hasBadge Then
                Dim br2 As New Rectangle(startX + tw + 6, r.Y + CInt((r.Height - 20) / 2), 20, 20)
                Using b As New SolidBrush(If(sel, Color.White, Theme.Primary))
                    g.FillEllipse(b, br2)
                End Using
                TextRenderer.DrawText(g, _badges(i).ToString(), Theme.F(8.5F, True), br2, If(sel, Theme.Primary, Color.White), TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            End If
        Next
    End Sub
End Class

''' <summary>Info about one banner slide (set up by the admin in the Banners tab).</summary>
Public Class BannerSlide
    Public Property ImagePath As String
    ''' <summary>Name the admin gave the banner. Only used for the headline when Headline is Nothing (old code that does not know headlines).</summary>
    Public Property Title As String
    Public Property Subtitle As String
    Public Property StallId As Integer
    ''' <summary>Big text on the banner. Nothing = use Title; empty text = no headline.</summary>
    Public Property Headline As String = Nothing
    ''' <summary>Photo position (0 to 1) and zoom (1 or more) chosen by the admin.</summary>
    Public Property FocusX As Double = 0.5
    Public Property FocusY As Double = 0.5
    Public Property Zoom As Double = 1.0
End Class

''' <summary>Sideways-scrolling ad banner. Auto-advances, can be dragged, slides are clickable. A search box can be placed on top as a child control.</summary>
Public Class BannerCarousel
    Inherits Control

    Private ReadOnly _slides As New List(Of BannerSlide)
    Private _pos As Double = 0
    Private _target As Double = 0
    Private _animating As Boolean
    Private ReadOnly _anim As New Timer()
    Private ReadOnly _auto As New Timer()
    Private _dragging As Boolean
    Private _moved As Boolean
    Private _downX As Integer
    Private _downPos As Double
    Public Event SlideClicked As EventHandler(Of BannerSlide)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Size = New Size(900, 260)
        _anim.Interval = 15
        AddHandler _anim.Tick, AddressOf AnimTick
        _auto.Interval = 4500
        AddHandler _auto.Tick, AddressOf AutoTick
    End Sub

    Public Sub SetSlides(slides As IEnumerable(Of BannerSlide))
        _slides.Clear()
        _slides.AddRange(slides)
        _pos = 0
        _target = 0
        _auto.Stop()
        If _slides.Count > 1 Then _auto.Start()
        Invalidate()
    End Sub

    Private Function SlideWidth() As Integer
        Return Width
    End Function

    Private Function Gap() As Integer
        Return 16
    End Function

    Private Function MaxPos() As Double
        Dim total As Double = _slides.Count * (SlideWidth() + Gap()) - Gap()
        Return Math.Max(0, total - Width)
    End Function

    Private Function SnapPos(i As Integer) As Double
        Return Math.Min(i * (SlideWidth() + Gap()), MaxPos())
    End Function

    Private Function NearestIndex() As Integer
        Dim best As Integer = 0
        Dim bestD As Double = Double.MaxValue
        For i As Integer = 0 To _slides.Count - 1
            Dim d As Double = Math.Abs(SnapPos(i) - _pos)
            If d < bestD Then
                bestD = d
                best = i
            End If
        Next
        Return best
    End Function

    Private Sub GoTo_(index As Integer)
        If _slides.Count = 0 Then Return
        index = ((index Mod _slides.Count) + _slides.Count) Mod _slides.Count
        _target = SnapPos(index)
        _animating = True
        _anim.Start()
    End Sub

    Private Sub AutoTick(sender As Object, e As EventArgs)
        If _dragging OrElse Not Visible OrElse _slides.Count < 2 Then Return
        GoTo_(NearestIndex() + 1)
    End Sub

    Private Sub AnimTick(sender As Object, e As EventArgs)
        If Not _animating Then
            _anim.Stop()
            Return
        End If
        _pos += (_target - _pos) * 0.16
        If Math.Abs(_target - _pos) < 0.6 Then
            _pos = _target
            _animating = False
            _anim.Stop()
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        _dragging = True
        _moved = False
        _animating = False
        _anim.Stop()
        _downX = e.X
        _downPos = _pos
        _auto.Stop()
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _dragging Then
            Dim dx As Integer = e.X - _downX
            If Math.Abs(dx) > 5 Then _moved = True
            If _moved Then
                _pos = Math.Max(0, Math.Min(MaxPos(), _downPos - dx))
                Invalidate()
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If Not _dragging Then Return
        _dragging = False
        If _moved Then
            Dim dx As Integer = e.X - _downX
            Dim idx As Integer = NearestIndex()
            If dx < -40 AndAlso _pos > _downPos Then
                idx = Math.Min(_slides.Count - 1, Math.Max(idx, NearestIndexFrom(_downPos) + 1))
            ElseIf dx > 40 AndAlso _pos < _downPos Then
                idx = Math.Max(0, Math.Min(idx, NearestIndexFrom(_downPos) - 1))
            End If
            _target = SnapPos(idx)
            _animating = True
            _anim.Start()
        Else
            For i As Integer = 0 To _slides.Count - 1
                Dim x As Double = i * (SlideWidth() + Gap()) - _pos
                If e.X >= x AndAlso e.X <= x + SlideWidth() Then
                    RaiseEvent SlideClicked(Me, _slides(i))
                    Exit For
                End If
            Next
        End If
        If _slides.Count > 1 Then
            _auto.Stop()
            _auto.Start()
        End If
    End Sub

    Private Function NearestIndexFrom(pos As Double) As Integer
        Dim keep As Double = _pos
        _pos = pos
        Dim n As Integer = NearestIndex()
        _pos = keep
        Return n
    End Function

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        _pos = Math.Min(_pos, MaxPos())
        _target = Math.Min(_target, MaxPos())
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _anim.Stop()
            _auto.Stop()
            _anim.Dispose()
            _auto.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        If _slides.Count = 0 Then
            Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), 22), lg As New LinearGradientBrush(New Rectangle(0, 0, Width, Height), Theme.Primary, Color.FromArgb(255, 150, 80), 20.0F)
                g.FillPath(lg, path)
            End Using
            TextRenderer.DrawText(g, "Welcome to " & Theme.AppName, Theme.F(22, True), New Rectangle(32, Height - 90, Width - 64, 50), Color.White, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Return
        End If
        Dim sw As Integer = SlideWidth()
        For i As Integer = 0 To _slides.Count - 1
            Dim x As Integer = CInt(i * (sw + Gap()) - _pos)
            If x > Width OrElse x + sw < 0 Then Continue For
            PaintSlide(g, _slides(i), New Rectangle(x, 0, sw, Height), i)
        Next
    End Sub

    Private Sub PaintSlide(g As Graphics, s As BannerSlide, r As Rectangle, index As Integer)
        Dim img As Image = Ui.LoadImage(s.ImagePath)
        Dim headline As String = If(s.Headline IsNot Nothing, s.Headline, s.Title)
        Dim opensStall As Boolean = (s.StallId > 0) OrElse Not String.IsNullOrEmpty(s.Subtitle)
        BannerPaint.Draw(g, New Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), img, s.FocusX, s.FocusY, s.Zoom,
                         headline, If(s.Title, "") & index.ToString(), opensStall, False, 22)
    End Sub
End Class
