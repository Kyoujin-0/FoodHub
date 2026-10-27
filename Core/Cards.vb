Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Module Painting
    ''' <summary>Renders an image cropped to cover w x h (or a gradient placeholder with initials) into a bitmap.</summary>
    Public Function RenderCover(img As Image, w As Integer, h As Integer, seed As String) As Bitmap
        Dim bmp As New Bitmap(Math.Max(1, w), Math.Max(1, h))
        Using g As Graphics = Graphics.FromImage(bmp)
            g.SmoothingMode = SmoothingMode.AntiAlias
            If img IsNot Nothing Then
                Dim scale As Double = Math.Max(w / CDbl(img.Width), h / CDbl(img.Height))
                Dim dw As Integer = CInt(Math.Ceiling(img.Width * scale))
                Dim dh As Integer = CInt(Math.Ceiling(img.Height * scale))
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.DrawImage(img, CInt((w - dw) / 2), CInt((h - dh) / 2), dw, dh)
            Else
                Dim cols As Color() = Theme.PlaceholderColors(seed)
                Using lg As New LinearGradientBrush(New Rectangle(0, 0, bmp.Width, bmp.Height), cols(0), cols(1), 45.0F)
                    g.FillRectangle(lg, 0, 0, bmp.Width, bmp.Height)
                End Using
                Dim px As Single = CSng(Math.Min(w, h) * 0.38)
                Using fnt As New Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel), sf As New StringFormat(), tb As New SolidBrush(Color.FromArgb(215, 255, 255, 255))
                    sf.Alignment = StringAlignment.Center
                    sf.LineAlignment = StringAlignment.Center
                    g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                    g.DrawString(Theme.Initials(seed), fnt, tb, New RectangleF(0, 0, bmp.Width, bmp.Height), sf)
                End Using
            End If
        End Using
        Return bmp
    End Function

    Public Sub DrawCardShadow(g As Graphics, w As Integer, h As Integer, radius As Integer)
        Using sp As GraphicsPath = Theme.RoundRectPath(New Rectangle(1, 3, w - 3, h - 4), radius), sb As New SolidBrush(Color.FromArgb(22, 60, 40, 20))
            g.FillPath(sb, sp)
        End Using
    End Sub
End Module

''' <summary>Stall tile for the Home page: cover photo, logo, name, category and open/closed status.</summary>
Public Class StallCard
    Inherits Control

    Private Const CoverH As Integer = 118
    Private _cover As Bitmap
    Private _logo As Bitmap
    Private _hover As Boolean
    Private ReadOnly _name As String
    Private ReadOnly _category As String
    Private ReadOnly _open As Boolean
    Private ReadOnly _coverImg As Image
    Private ReadOnly _logoImg As Image
    Public ReadOnly StallId As Integer

    Public Sub New(stallId As Integer, name As String, category As String, isOpen As Boolean, coverImg As Image, logoImg As Image)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Me.StallId = stallId
        _name = name
        _category = category
        _open = isOpen
        _coverImg = coverImg
        _logoImg = logoImg
        Cursor = Cursors.Hand
        Size = New Size(230, 214)
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

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If _cover IsNot Nothing Then
            _cover.Dispose()
            _cover = Nothing
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If _cover IsNot Nothing Then _cover.Dispose()
            If _logo IsNot Nothing Then _logo.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        If Width < 40 OrElse Height < 60 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim cw As Integer = Width - 1
        Painting.DrawCardShadow(g, Width, Height, 16)
        Dim cardRect As New Rectangle(0, 0, cw, Height - 5)
        Using path As GraphicsPath = Theme.RoundRectPath(cardRect, 16), br As New SolidBrush(Color.White)
            g.FillPath(br, path)
        End Using
        If _cover Is Nothing Then _cover = Painting.RenderCover(_coverImg, cw, CoverH, _name & "cover")
        If _logo Is Nothing Then _logo = Painting.RenderCover(_logoImg, 58, 58, _name)
        Using cpath As GraphicsPath = Theme.RoundTopPath(New Rectangle(0, 0, cw, CoverH), 16)
            Using tb As New TextureBrush(_cover)
                g.FillPath(tb, cpath)
            End Using
            If Not _open Then
                Using dim1 As New SolidBrush(Color.FromArgb(95, 20, 12, 8))
                    g.FillPath(dim1, cpath)
                End Using
            End If
        End Using

        ' status pill
        Dim st As String = If(_open, "Open", "Closed")
        Dim pf As Font = Theme.F(8.5F, True)
        Dim pw As Integer = TextRenderer.MeasureText(st, pf, New Size(200, 40), TextFormatFlags.NoPadding).Width + 24
        Dim pr As New Rectangle(cw - 12 - pw, 12, pw, 24)
        Using pp As GraphicsPath = Theme.RoundRectPath(pr, 12), pb As New SolidBrush(Color.FromArgb(245, 255, 255, 255))
            g.FillPath(pb, pp)
        End Using
        Using db As New SolidBrush(If(_open, Theme.Success, Theme.Danger))
            g.FillEllipse(db, pr.X + 9, pr.Y + 9, 7, 7)
        End Using
        TextRenderer.DrawText(g, st, pf, New Rectangle(pr.X + 19, pr.Y, pr.Width - 19, pr.Height), If(_open, Theme.Success, Theme.Danger), TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        ' logo with white ring
        Dim lx As Integer = 16
        Dim ly As Integer = CoverH - 29
        Using wb As New SolidBrush(Color.White)
            g.FillEllipse(wb, lx - 4, ly - 4, 66, 66)
        End Using
        Using tb As New TextureBrush(_logo)
            tb.TranslateTransform(lx, ly)
            g.FillEllipse(tb, lx, ly, 58, 58)
        End Using

        ' texts
        TextRenderer.DrawText(g, _name, Theme.F(11.5F, True), New Rectangle(16, CoverH + 36, cw - 32, 24), Theme.TextDark, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        TextRenderer.DrawText(g, _category, Theme.F(9.5F), New Rectangle(16, CoverH + 60, cw - 32, 20), Theme.TextMuted, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)

        If _hover Then
            Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(1, 1, cw - 2, Height - 7), 16), pen As New Pen(Theme.Primary, 2.0F)
                g.DrawPath(pen, path)
            End Using
        End If
    End Sub
End Class

''' <summary>Product tile for the stall menu: photo, title, rating, description, price and an add / options button.</summary>
Public Class ProductCard
    Inherits Control

    Private Const ImgH As Integer = 200
    Private _cover As Bitmap
    Private _hover As Boolean
    Private ReadOnly _btn As New RoundedButton()
    Private ReadOnly _img As Image
    Private ReadOnly _name As String
    Private ReadOnly _desc As String
    Private ReadOnly _price As Decimal
    Private ReadOnly _hasSizes As Boolean
    Private ReadOnly _avg As Double
    Private ReadOnly _count As Integer
    Private ReadOnly _stallName As String
    Private ReadOnly _showStall As Boolean
    Private ReadOnly _available As Boolean
    Private ReadOnly _stallOpen As Boolean
    Public ReadOnly ProductId As Integer
    Public ReadOnly NeedsOptions As Boolean
    Public ReadOnly StallOpen As Boolean
    Public Event AddClicked As EventHandler

    Public Sub New(productId As Integer, name As String, shortDesc As String, price As Decimal, hasSizes As Boolean, hasOptions As Boolean,
                   avg As Double, count As Integer, stallName As String, showStall As Boolean, available As Boolean, stallOpen As Boolean, img As Image)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Me.ProductId = productId
        _name = name
        _desc = Ui.Ellipsize(shortDesc, 80)
        _price = price
        _hasSizes = hasSizes
        _avg = avg
        _count = count
        _stallName = stallName
        _showStall = showStall
        _available = available
        _stallOpen = stallOpen
        _img = img
        NeedsOptions = hasOptions
        Me.StallOpen = stallOpen
        Cursor = Cursors.Hand
        Size = New Size(270, 398)

        If Not available Then
            _btn.Text = "Unavailable"
            _btn.Enabled = False
        ElseIf Not stallOpen Then
            _btn.Text = "Stall closed"
            _btn.Enabled = False
        ElseIf hasOptions Then
            _btn.Text = "Select options"
            _btn.FillColor = Color.White
            _btn.HoverColor = Theme.PrimarySoft
            _btn.TextColor = Theme.Primary
            _btn.BorderColor = Theme.Primary
        Else
            _btn.Text = "Add to order"
        End If
        _btn.Radius = 12
        AddHandler _btn.Click, Sub(s, e) RaiseEvent AddClicked(Me, EventArgs.Empty)
        Controls.Add(_btn)
        PositionButton()
    End Sub

    Private Sub PositionButton()
        _btn.SetBounds(16, Height - 5 - 16 - 42, Math.Max(40, Width - 33), 42)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        PositionButton()
        If _cover IsNot Nothing Then
            _cover.Dispose()
            _cover = Nothing
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If Not _hover Then
            _hover = True
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hover AndAlso Not ClientRectangle.Contains(PointToClient(Cursor.Position)) Then
            _hover = False
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso _cover IsNot Nothing Then _cover.Dispose()
        MyBase.Dispose(disposing)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        If Width < 60 OrElse Height < 100 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim cw As Integer = Width - 1
        Painting.DrawCardShadow(g, Width, Height, 16)
        Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, cw, Height - 5), 16), br As New SolidBrush(Color.White)
            g.FillPath(br, path)
        End Using
        If _cover Is Nothing Then _cover = Painting.RenderCover(_img, cw, ImgH, _name)
        Using cpath As GraphicsPath = Theme.RoundTopPath(New Rectangle(0, 0, cw, ImgH), 16)
            Using tb As New TextureBrush(_cover)
                g.FillPath(tb, cpath)
            End Using
            If Not _available OrElse Not _stallOpen Then
                Using dim1 As New SolidBrush(Color.FromArgb(120, 255, 255, 255))
                    g.FillPath(dim1, cpath)
                End Using
            End If
        End Using
        If Not _available Then
            DrawBadge(g, "Sold out", Theme.Danger, 12, 12)
        End If

        TextRenderer.DrawText(g, _name, Theme.F(11.5F, True), New Rectangle(16, ImgH + 12, cw - 32, 24), Theme.TextDark, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)

        ' rating row
        Dim ry As Integer = ImgH + 40
        Dim ratingW As Integer
        If _count > 0 Then
            Icons.Draw(g, "star", New Rectangle(16, ry + 2, 15, 15), Theme.Star)
            TextRenderer.DrawText(g, _avg.ToString("0.0"), Theme.F(9.5F, True), New Rectangle(36, ry, 30, 19), Theme.TextDark, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Dim cnt As String = "(" & _count.ToString() & ")"
            TextRenderer.DrawText(g, cnt, Theme.F(9), New Rectangle(62, ry, 50, 19), Theme.TextMuted, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            ratingW = 112
        Else
            Icons.Draw(g, "star", New Rectangle(16, ry + 2, 15, 15), Color.FromArgb(210, 204, 196))
            TextRenderer.DrawText(g, "No reviews", Theme.F(9), New Rectangle(36, ry, 90, 19), Theme.TextMuted, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            ratingW = 126
        End If
        If _showStall Then
            TextRenderer.DrawText(g, _stallName, Theme.F(8.5F), New Rectangle(16 + ratingW, ry, cw - 32 - ratingW, 19), Theme.TextMuted, TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        End If

        TextRenderer.DrawText(g, _desc, Theme.F(9), New Rectangle(16, ImgH + 66, cw - 32, 38), Theme.TextMuted, TextFormatFlags.Left Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)

        ' price
        Dim px As Integer = 16
        If _hasSizes Then
            TextRenderer.DrawText(g, "From ", Theme.F(9.5F), New Rectangle(16, ImgH + 108, 40, 24), Theme.TextMuted, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            px = 52
        End If
        TextRenderer.DrawText(g, Theme.Peso(_price), Theme.F(14, True), New Rectangle(px, ImgH + 106, cw - px - 16, 28), Theme.Primary, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        If _hover Then
            Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(1, 1, cw - 2, Height - 7), 16), pen As New Pen(Color.FromArgb(120, Theme.Primary), 2.0F)
                g.DrawPath(pen, path)
            End Using
        End If
    End Sub

    Private Sub DrawBadge(g As Graphics, text As String, col As Color, x As Integer, y As Integer)
        Dim f As Font = Theme.F(8.5F, True)
        Dim w As Integer = TextRenderer.MeasureText(text, f, New Size(300, 40), TextFormatFlags.NoPadding).Width + 22
        Dim r As New Rectangle(x, y, w, 24)
        Using p As GraphicsPath = Theme.RoundRectPath(r, 12), b As New SolidBrush(col)
            g.FillPath(b, p)
        End Using
        TextRenderer.DrawText(g, text, f, r, Color.White, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
    End Sub
End Class
