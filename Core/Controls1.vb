Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Rounded card. BackColor == FillColor so child controls blend in; SurroundColor paints the corners.</summary>
Public Class RoundedPanel
    Inherits Panel

    Private _fill As Color = Color.White
    Private _surround As Color = Theme.Bg
    Public Property Radius As Integer = 14
    Public Property BorderColor As Color = Color.Empty
    Public Property Shadow As Boolean = False

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        MyBase.BackColor = _fill
    End Sub

    Public Property FillColor As Color
        Get
            Return _fill
        End Get
        Set(value As Color)
            _fill = value
            MyBase.BackColor = value
            Invalidate()
        End Set
    End Property

    Public Property SurroundColor As Color
        Get
            Return _surround
        End Get
        Set(value As Color)
            _surround = value
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(_surround)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim h As Integer = Height - 1
        If Shadow Then
            Using sp As GraphicsPath = Theme.RoundRectPath(New Rectangle(1, 3, Width - 3, Height - 4), Radius), sb As New SolidBrush(Color.FromArgb(22, 60, 40, 20))
                g.FillPath(sb, sp)
            End Using
            h = Height - 4
        End If
        Dim rc As New Rectangle(0, 0, Width - 1, h)
        Using path As GraphicsPath = Theme.RoundRectPath(rc, Radius), br As New SolidBrush(_fill)
            g.FillPath(br, path)
            If Not BorderColor.IsEmpty Then
                Using pen As New Pen(BorderColor, 1)
                    g.DrawPath(pen, path)
                End Using
            End If
        End Using
    End Sub
End Class

''' <summary>Flat rounded button with optional icon. Use FillColor/HoverColor/TextColor/BorderColor to restyle.</summary>
Public Class RoundedButton
    Inherits Control

    Private _hover As Boolean
    Private _down As Boolean
    Public Property Radius As Integer = 12
    Public Property FillColor As Color = Theme.Primary
    Public Property HoverColor As Color = Theme.PrimaryDark
    Public Property TextColor As Color = Color.White
    Public Property BorderColor As Color = Color.Empty
    Public Property IconName As String = ""

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(140, 42)
        Font = Theme.F(10, True)
        Cursor = Cursors.Hand
    End Sub

    Protected Overrides Sub OnTextChanged(e As EventArgs)
        MyBase.OnTextChanged(e)
        Invalidate()
    End Sub

    Protected Overrides Sub OnEnabledChanged(e As EventArgs)
        MyBase.OnEnabledChanged(e)
        Cursor = If(Enabled, Cursors.Hand, Cursors.Default)
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        _hover = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _hover = False
        _down = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        _down = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _down = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim fill As Color = FillColor
        Dim fore As Color = TextColor
        Dim border As Color = BorderColor
        If Not Enabled Then
            fill = Color.FromArgb(234, 229, 222)
            fore = Color.FromArgb(160, 154, 146)
            border = Color.Empty
        ElseIf _down Then
            fill = ControlPaint.Dark(HoverColor, 0.05F)
        ElseIf _hover Then
            fill = HoverColor
        End If
        Dim rc As New Rectangle(0, 0, Width - 1, Height - 1)
        Using path As GraphicsPath = Theme.RoundRectPath(rc, Radius)
            Using br As New SolidBrush(fill)
                g.FillPath(br, path)
            End Using
            If Not border.IsEmpty Then
                Using pen As New Pen(border, 1.5F)
                    g.DrawPath(pen, path)
                End Using
            End If
        End Using
        Dim tw As Integer = TextRenderer.MeasureText(g, Text, Font, New Size(10000, 100), TextFormatFlags.NoPadding).Width
        Dim iconW As Integer = 0
        If IconName <> "" Then iconW = 18 + 6
        Dim startX As Integer = CInt((Width - (tw + iconW)) / 2)
        If startX < 8 Then startX = 8
        If IconName <> "" Then
            Icons.Draw(g, IconName, New Rectangle(startX, CInt((Height - 18) / 2), 18, 18), fore)
        End If
        Dim tr As New Rectangle(startX + iconW, 0, Math.Max(10, Width - startX - iconW - 6), Height)
        TextRenderer.DrawText(g, Text, Font, tr, fore, TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
    End Sub
End Class

''' <summary>Small square/round button that only shows an icon.</summary>
Public Class IconButton
    Inherits Control

    Private _hover As Boolean
    Public Property IconName As String = "x"
    Public Property IconColor As Color = Theme.TextMuted
    Public Property HoverIconColor As Color = Theme.Primary
    Public Property HoverBack As Color = Theme.PrimarySoft
    Public Property Filled As Boolean = False
    Public Property BorderColor As Color = Color.Empty

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(34, 34)
        Cursor = Cursors.Hand
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        _hover = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _hover = False
        Invalidate()
    End Sub

    Public Sub Redraw()
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim rc As New Rectangle(0, 0, Width - 1, Height - 1)
        If _hover Then
            Using path As GraphicsPath = Theme.RoundRectPath(rc, Height \ 2), br As New SolidBrush(HoverBack)
                g.FillPath(br, path)
            End Using
        End If
        If Not BorderColor.IsEmpty Then
            Using path As GraphicsPath = Theme.RoundRectPath(rc, Height \ 2), pen As New Pen(BorderColor, 1.5F)
                g.DrawPath(pen, path)
            End Using
        End If
        Dim sz As Integer = CInt(Math.Min(Width, Height) * 0.52)
        Dim ir As New Rectangle(CInt((Width - sz) / 2), CInt((Height - sz) / 2), sz, sz)
        Icons.Draw(g, IconName, ir, If(_hover, HoverIconColor, IconColor), Filled)
    End Sub
End Class

''' <summary>Custom checkbox (rounded square).</summary>
Public Class RoundCheck
    Inherits Control

    Private _checked As Boolean
    Public Event CheckedChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(22, 22)
        Cursor = Cursors.Hand
    End Sub

    Public Property Checked As Boolean
        Get
            Return _checked
        End Get
        Set(value As Boolean)
            If _checked <> value Then
                _checked = value
                Invalidate()
                RaiseEvent CheckedChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    ''' <summary>Set without raising CheckedChanged.</summary>
    Public Sub SetCheckedSilently(value As Boolean)
        _checked = value
        Invalidate()
    End Sub

    Protected Overrides Sub OnClick(e As EventArgs)
        MyBase.OnClick(e)
        Checked = Not _checked
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim s As Integer = Math.Min(Width, Height) - 2
        Dim rc As New Rectangle(1, 1, s - 1, s - 1)
        Using path As GraphicsPath = Theme.RoundRectPath(rc, 6)
            If _checked Then
                Using br As New SolidBrush(Theme.Primary)
                    g.FillPath(br, path)
                End Using
                Icons.Draw(g, "check", New Rectangle(rc.X + 3, rc.Y + 3, rc.Width - 6, rc.Height - 6), Color.White)
            Else
                Using br As New SolidBrush(Color.White), pen As New Pen(Color.FromArgb(205, 197, 188), 1.5F)
                    g.FillPath(br, path)
                    g.DrawPath(pen, path)
                End Using
            End If
        End Using
    End Sub
End Class

''' <summary>Small coloured badge (Open/Closed, order status...).</summary>
Public Class PillLabel
    Inherits Control

    Public Property FillColor As Color = Theme.PrimarySoft
    Public Property TextColor As Color = Theme.Primary

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Font = Theme.F(8.5F, True)
        Size = New Size(70, 24)
    End Sub

    Protected Overrides Sub OnTextChanged(e As EventArgs)
        MyBase.OnTextChanged(e)
        Invalidate()
    End Sub

    ''' <summary>Resize to fit the text.</summary>
    Public Sub FitToText(Optional padX As Integer = 11)
        Dim tw As Integer = TextRenderer.MeasureText(Text, Font, New Size(10000, 100), TextFormatFlags.NoPadding).Width
        Width = tw + padX * 2
        Height = Font.Height + 8
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), Height \ 2), br As New SolidBrush(FillColor)
            g.FillPath(br, path)
        End Using
        TextRenderer.DrawText(g, Text, Font, New Rectangle(0, 0, Width, Height), TextColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub
End Class

''' <summary>Picture that fills its box (crops to cover). Falls back to a coloured placeholder with initials.</summary>
Public Class CoverImage
    Inherits Control

    Private _img As Image
    Private _cover As Bitmap
    Private _placeholder As String = ""
    Public Property Radius As Integer = 14
    Public Property TopOnly As Boolean = False
    Public Property Circle As Boolean = False

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(100, 100)
    End Sub

    Public Property Image As Image
        Get
            Return _img
        End Get
        Set(value As Image)
            _img = value
            ResetCover()
        End Set
    End Property

    Public Property PlaceholderText As String
        Get
            Return _placeholder
        End Get
        Set(value As String)
            _placeholder = value
            ResetCover()
        End Set
    End Property

    Private Sub ResetCover()
        If _cover IsNot Nothing Then
            _cover.Dispose()
            _cover = Nothing
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        ResetCover()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso _cover IsNot Nothing Then
            _cover.Dispose()
            _cover = Nothing
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private Sub BuildCover()
        If Width < 2 OrElse Height < 2 Then Return
        _cover = New Bitmap(Width, Height)
        Using g As Graphics = Graphics.FromImage(_cover)
            g.SmoothingMode = SmoothingMode.AntiAlias
            If _img IsNot Nothing Then
                Dim scale As Double = Math.Max(Width / CDbl(_img.Width), Height / CDbl(_img.Height))
                Dim dw As Integer = CInt(Math.Ceiling(_img.Width * scale))
                Dim dh As Integer = CInt(Math.Ceiling(_img.Height * scale))
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.DrawImage(_img, CInt((Width - dw) / 2), CInt((Height - dh) / 2), dw, dh)
            Else
                Dim cols As Color() = Theme.PlaceholderColors(_placeholder)
                Using lg As New LinearGradientBrush(New Rectangle(0, 0, Width, Height), cols(0), cols(1), 45.0F)
                    g.FillRectangle(lg, 0, 0, Width, Height)
                End Using
                Dim px As Single = CSng(Math.Min(Width, Height) * 0.38)
                Using fnt As New Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel), sf As New StringFormat(), tb As New SolidBrush(Color.FromArgb(215, 255, 255, 255))
                    sf.Alignment = StringAlignment.Center
                    sf.LineAlignment = StringAlignment.Center
                    g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                    g.DrawString(Theme.Initials(_placeholder), fnt, tb, New RectangleF(0, 0, Width, Height), sf)
                End Using
            End If
        End Using
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        If Width < 2 OrElse Height < 2 Then Return
        If _cover Is Nothing Then BuildCover()
        If _cover Is Nothing Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim rc As New Rectangle(0, 0, Width, Height)
        Dim path As GraphicsPath
        If Circle Then
            path = New GraphicsPath()
            path.AddEllipse(rc)
        ElseIf TopOnly Then
            path = Theme.RoundTopPath(rc, Radius)
        Else
            path = Theme.RoundRectPath(rc, Radius)
        End If
        Using path
            Using tb As New TextureBrush(_cover)
                g.FillPath(tb, path)
            End Using
        End Using
    End Sub
End Class

''' <summary>[ - ] 2 [ + ] stepper.</summary>
Public Class QtyStepper
    Inherits Control

    Private _value As Integer = 1
    Public Property Minimum As Integer = 1
    Public Property Maximum As Integer = 99
    Public Event ValueChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(112, 36)
        Font = Theme.F(10.5F, True)
        Cursor = Cursors.Hand
    End Sub

    Public Property Value As Integer
        Get
            Return _value
        End Get
        Set(v As Integer)
            Dim nv As Integer = Math.Max(Minimum, Math.Min(Maximum, v))
            If nv <> _value Then
                _value = nv
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End If
        End Set
    End Property

    Public Sub SetValueSilently(v As Integer)
        _value = Math.Max(Minimum, Math.Min(Maximum, v))
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        If e.X < Height Then
            Value = _value - 1
        ElseIf e.X > Width - Height Then
            Value = _value + 1
        End If
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), Height \ 2), pen As New Pen(Theme.Border, 1.5F), br As New SolidBrush(Color.White)
            g.FillPath(br, path)
            g.DrawPath(pen, path)
        End Using
        Dim ic As Integer = CInt(Height * 0.42)
        Dim top As Integer = CInt((Height - ic) / 2)
        Icons.Draw(g, "minus", New Rectangle(CInt((Height - ic) / 2), top, ic, ic), If(_value <= Minimum, Color.FromArgb(200, 194, 186), Theme.TextDark))
        Icons.Draw(g, "plus", New Rectangle(Width - Height + CInt((Height - ic) / 2), top, ic, ic), If(_value >= Maximum, Color.FromArgb(200, 194, 186), Theme.TextDark))
        TextRenderer.DrawText(g, _value.ToString(), Font, New Rectangle(Height, 0, Width - Height * 2, Height), Theme.TextDark, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
    End Sub
End Class

''' <summary>Star + score + review count.</summary>
Public Class RatingLabel
    Inherits Control

    Public Property Score As Double = 0
    Public Property Count As Integer = 0

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(140, 20)
        Font = Theme.F(9)
    End Sub

    Public Sub SetRating(score As Double, count As Integer)
        Me.Score = score
        Me.Count = count
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        Dim s As Integer = Height - 4
        If Count > 0 Then
            Icons.Draw(g, "star", New Rectangle(0, 2, s, s), Theme.Star)
            TextRenderer.DrawText(g, Score.ToString("0.0"), Theme.F(9, True), New Rectangle(s + 4, 0, 34, Height), Theme.TextDark, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Dim label As String = "(" & Count.ToString() & If(Count = 1, " review)", " reviews)")
            TextRenderer.DrawText(g, label, Font, New Rectangle(s + 30, 0, Width - s - 30, Height), Theme.TextMuted, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        Else
            Icons.Draw(g, "star", New Rectangle(0, 2, s, s), Color.FromArgb(210, 204, 196))
            TextRenderer.DrawText(g, "No reviews yet", Font, New Rectangle(s + 4, 0, Width - s - 4, Height), Theme.TextMuted, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End If
    End Sub
End Class
