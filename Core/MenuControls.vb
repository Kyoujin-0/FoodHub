Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Windows.Forms

''' <summary>Small helpers used by the seller Menu tab.</summary>
Public Module MenuFmt
    Public Function TryPrice(text As String, ByRef value As Decimal) As Boolean
        Dim t As String = If(text, "").Trim().Replace(ChrW(&H20B1), "").Replace(",", "").Trim()
        If t = "" Then Return False
        If Not Decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, value) Then Return False
        If value < 0 OrElse value > 99999 Then Return False
        value = Math.Round(value, 2)
        Return True
    End Function

    Public Function TryInt(text As String, ByRef value As Integer, minV As Integer, maxV As Integer) As Boolean
        Dim t As String = If(text, "").Trim()
        If t = "" Then Return False
        If Not Integer.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, value) Then Return False
        Return value >= minV AndAlso value <= maxV
    End Function

    Public Function FormatNum(d As Decimal) As String
        If d = Math.Floor(d) Then Return d.ToString("0", CultureInfo.InvariantCulture)
        Return d.ToString("0.00", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>Only digits (and one dot when allowed) can stay in the box.</summary>
    Public Sub NumericOnly(box As AppTextBox, allowDot As Boolean)
        Dim busy As Boolean = False
        AddHandler box.TextChanged,
            Sub(s, e)
                If busy Then Return
                Dim t As String = box.Text
                Dim sb As New StringBuilder()
                Dim dot As Boolean = False
                For Each ch As Char In t
                    If Char.IsDigit(ch) Then
                        sb.Append(ch)
                    ElseIf allowDot AndAlso ch = "."c AndAlso Not dot Then
                        sb.Append(ch)
                        dot = True
                    End If
                Next
                If sb.ToString() <> t Then
                    busy = True
                    box.Text = sb.ToString()
                    busy = False
                End If
            End Sub
    End Sub

    Public Function PickImage(owner As IWin32Window) As String
        Using dlg As New OpenFileDialog()
            dlg.Title = "Choose a photo"
            dlg.Filter = "Images (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*"
            If dlg.ShowDialog(owner) <> DialogResult.OK Then Return ""
            If New FileInfo(dlg.FileName).Length > 20 * 1024 * 1024 Then
                Ui.Warn("That photo is too big. Please pick one under 20 MB.")
                Return ""
            End If
            Return dlg.FileName
        End Using
    End Function

    ''' <summary>Copies (and shrinks) a chosen photo into images\products next to the .exe. Returns the relative path to save in the database.</summary>
    Public Function StoreProductImage(sourceFile As String) As String
        Dim dir As String = Path.Combine(Db.DataFolder(), "images", "products")
        Directory.CreateDirectory(dir)
        Dim fileName As String = Guid.NewGuid().ToString("N") & ".jpg"
        Dim full As String = Path.Combine(dir, fileName)
        Using src As Image = Image.FromStream(New MemoryStream(File.ReadAllBytes(sourceFile)))
            Dim sc As Double = Math.Min(1.0, 1100.0 / Math.Max(src.Width, src.Height))
            Dim nw As Integer = Math.Max(1, CInt(src.Width * sc))
            Dim nh As Integer = Math.Max(1, CInt(src.Height * sc))
            Using bmp As New Bitmap(nw, nh)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.White)
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    g.DrawImage(src, 0, 0, nw, nh)
                End Using
                bmp.Save(full, ImageFormat.Jpeg)
            End Using
        End Using
        Return "images/products/" & fileName
    End Function

    Public Sub DeleteProductImage(relPath As String)
        Try
            If String.IsNullOrEmpty(relPath) OrElse Not relPath.StartsWith("images/products/") Then Return
            Dim full As String = Path.Combine(Db.DataFolder(), relPath)
            If File.Exists(full) Then File.Delete(full)
        Catch
        End Try
    End Sub
End Module

''' <summary>Draws the on/off switch used by the menu cards and the Track stock toggle.</summary>
Public Module SwitchPaint
    Public Sub Draw(g As Graphics, r As Rectangle, isOn As Boolean)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(r, r.Height \ 2), b As New SolidBrush(If(isOn, Theme.Success, Color.FromArgb(208, 201, 192)))
            g.FillPath(b, p)
        End Using
        Dim d As Integer = r.Height - 8
        Dim kx As Integer = If(isOn, r.Right - d - 4, r.X + 4)
        Using b As New SolidBrush(Color.White)
            g.FillEllipse(b, kx, r.Y + 4, d, d)
        End Using
    End Sub
End Module

Public Class ToggleSwitch
    Inherits Control

    Private _checked As Boolean
    Public Event CheckedChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Size = New Size(54, 30)
        Cursor = Cursors.Hand
    End Sub

    Public Property Checked As Boolean
        Get
            Return _checked
        End Get
        Set(value As Boolean)
            If _checked = value Then Return
            _checked = value
            Invalidate()
            RaiseEvent CheckedChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.Clear(BackColor)
        SwitchPaint.Draw(e.Graphics, New Rectangle(0, 0, Width - 1, Height - 1), _checked)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button = MouseButtons.Left Then Checked = Not _checked
    End Sub
End Class

Public Enum ChipMode
    Toggle
    Tags
End Enum

''' <summary>Wrapping chips. Toggle mode: click to select / unselect (extras). Tags mode: each chip has an x (removable ingredients).</summary>
Public Class ChipFlow
    Inherits Control

    Public Mode As ChipMode = ChipMode.Toggle
    Public ReadOnly Items As New List(Of String)
    Public ReadOnly Selected As New HashSet(Of Integer)
    Public Event Changed As EventHandler
    Public Event TagRemoved(index As Integer)

    Private ReadOnly _rects As New List(Of Rectangle)
    Private ReadOnly _f As Font = Theme.F(10)
    Private _h As Integer
    Private _hot As Integer = -1

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
    End Sub

    Private Sub LayoutChips(width As Integer)
        _rects.Clear()
        Dim x As Integer = 0
        Dim y As Integer = 0
        Dim h As Integer = 36
        For Each t As String In Items
            Dim w As Integer = TextRenderer.MeasureText(t, _f, New Size(1000, 50), TextFormatFlags.NoPadding).Width + If(Mode = ChipMode.Tags, 44, 30)
            w = Math.Min(w, Math.Max(60, width))
            If x + w > width AndAlso x > 0 Then
                x = 0
                y += h + 8
            End If
            _rects.Add(New Rectangle(x, y, w, h))
            x += w + 8
        Next
        _h = If(Items.Count = 0, 0, y + h)
    End Sub

    Public Function RequiredHeight(width As Integer) As Integer
        LayoutChips(width)
        Return _h
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        LayoutChips(Width)
        For i As Integer = 0 To _rects.Count - 1
            Dim r As Rectangle = _rects(i)
            Dim on1 As Boolean = (Mode = ChipMode.Toggle AndAlso Selected.Contains(i))
            Dim fill As Color = If(Mode = ChipMode.Tags, Theme.FieldBg, If(on1, Theme.PrimarySoft, Color.White))
            Dim border As Color = If(on1, Theme.Primary, If(Mode = ChipMode.Tags, Theme.FieldBg, Theme.Border))
            Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), 18), b As New SolidBrush(fill), pen As New Pen(border, If(on1, 1.6F, 1.0F))
                g.FillPath(b, p)
                g.DrawPath(pen, p)
            End Using
            Dim textW As Integer = r.Width - If(Mode = ChipMode.Tags, 40, 24)
            TextRenderer.DrawText(g, Items(i), _f, New Rectangle(r.X + 14, r.Y, Math.Max(10, textW), r.Height), If(on1, Theme.Primary, Theme.TextDark),
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
            If Mode = ChipMode.Tags Then
                Icons.Draw(g, "x", New Rectangle(r.Right - 28, r.Y + 10, 16, 16), If(_hot = i, Theme.Danger, Theme.TextMuted))
            End If
        Next
    End Sub

    Private Function HitIndex(p As Point) As Integer
        For i As Integer = 0 To _rects.Count - 1
            If _rects(i).Contains(p) Then Return i
        Next
        Return -1
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim h As Integer = HitIndex(e.Location)
        If h <> _hot Then
            _hot = h
            Cursor = If(h >= 0, Cursors.Hand, Cursors.Default)
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _hot = -1
        Cursor = Cursors.Default
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button <> MouseButtons.Left Then Return
        Dim h As Integer = HitIndex(e.Location)
        If h < 0 Then Return
        If Mode = ChipMode.Toggle Then
            If Selected.Contains(h) Then Selected.Remove(h) Else Selected.Add(h)
            Invalidate()
            RaiseEvent Changed(Me, EventArgs.Empty)
        Else
            If e.X >= _rects(h).Right - 36 Then RaiseEvent TagRemoved(h)
        End If
    End Sub
End Class

''' <summary>Category chips with item counts, plus a "+" chip to add a category.</summary>
Public Class MenuChipBar
    Inherits Control

    Public Event SelectedChanged As EventHandler
    Public Event AddClicked As EventHandler

    Private ReadOnly _names As New List(Of String)
    Private ReadOnly _counts As New List(Of Integer)
    Private ReadOnly _rects As New List(Of Rectangle)
    Private ReadOnly _fName As Font = Theme.F(11.5F, True)
    Private ReadOnly _fCount As Font = Theme.F(9.5F)
    Private _addRect As Rectangle
    Private _sel As Integer
    Private _h As Integer = 44

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

    Private Sub LayoutChips(width As Integer)
        _rects.Clear()
        Dim x As Integer = 0
        Dim y As Integer = 0
        For i As Integer = 0 To _names.Count - 1
            Dim w As Integer = Measure(_names(i), _fName) + Measure(_counts(i).ToString(), _fCount) + 44
            If x + w > width AndAlso x > 0 Then
                x = 0
                y += 54
            End If
            _rects.Add(New Rectangle(x, y, w, 44))
            x += w + 10
        Next
        If x + 44 > width AndAlso x > 0 Then
            x = 0
            y += 54
        End If
        _addRect = New Rectangle(x, y, 44, 44)
        _h = y + 44
    End Sub

    Public Function RequiredHeight(width As Integer) As Integer
        LayoutChips(width)
        Return _h
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        LayoutChips(Width)
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
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(_addRect.X, _addRect.Y, _addRect.Width - 1, _addRect.Height - 1), 22), pen As New Pen(Color.FromArgb(214, 206, 196), 1.5F)
            pen.DashStyle = DashStyle.Dash
            g.DrawPath(pen, p)
        End Using
        Icons.Draw(g, "plus", New Rectangle(_addRect.X + 13, _addRect.Y + 13, 18, 18), Theme.TextMuted)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim over As Boolean = _addRect.Contains(e.Location)
        If Not over Then
            For Each r As Rectangle In _rects
                If r.Contains(e.Location) Then over = True
            Next
        End If
        Cursor = If(over, Cursors.Hand, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button <> MouseButtons.Left Then Return
        If _addRect.Contains(e.Location) Then
            RaiseEvent AddClicked(Me, EventArgs.Empty)
            Return
        End If
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

''' <summary>Menu item tile for the seller: photo, name, price, category, stock pill and the Available switch.</summary>
Public Class MenuItemCard
    Inherits Control

    Public ReadOnly ProductId As Integer
    Public Event CardClicked As EventHandler
    Public Event AvailabilityToggled As EventHandler

    Private ReadOnly _name As String
    Private ReadOnly _category As String
    Private ReadOnly _price As Decimal
    Private ReadOnly _hasVariants As Boolean
    Private ReadOnly _track As Boolean
    Private ReadOnly _stock As Integer
    Private ReadOnly _img As Image
    Private _available As Boolean
    Private _selected As Boolean
    Private _hover As Boolean
    Private _cover As Bitmap
    Private _toggleRect As Rectangle

    Public Sub New(productId As Integer, name As String, category As String, price As Decimal, hasVariants As Boolean,
                   track As Boolean, stock As Integer, available As Boolean, img As Image)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Me.ProductId = productId
        _name = name
        _category = category
        _price = price
        _hasVariants = hasVariants
        _track = track
        _stock = stock
        _available = available
        _img = img
        Cursor = Cursors.Hand
    End Sub

    Public Shared Function HeightFor(w As Integer) As Integer
        Return CInt(w * 0.72) + 196
    End Function

    Public Property Selected As Boolean
        Get
            Return _selected
        End Get
        Set(value As Boolean)
            If _selected = value Then Return
            _selected = value
            Invalidate()
        End Set
    End Property

    Public Property Available As Boolean
        Get
            Return _available
        End Get
        Set(value As Boolean)
            If _available = value Then Return
            _available = value
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If _cover IsNot Nothing Then
            _cover.Dispose()
            _cover = Nothing
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso _cover IsNot Nothing Then _cover.Dispose()
        MyBase.Dispose(disposing)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        If Width < 80 OrElse Height < 120 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim cw As Integer = Width - 1
        Dim imgH As Integer = CInt(Width * 0.72)
        Painting.DrawCardShadow(g, Width, Height, 18)
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, cw, Height - 5), 18)
            g.FillPath(Brushes.White, p)
        End Using
        If _cover Is Nothing Then _cover = Painting.RenderCover(_img, cw, imgH, _name)
        Using cp As GraphicsPath = Theme.RoundTopPath(New Rectangle(0, 0, cw, imgH), 18)
            Using tb As New TextureBrush(_cover)
                g.FillPath(tb, cp)
            End Using
            If Not _available Then
                Using dimB As New SolidBrush(Color.FromArgb(120, 255, 255, 255))
                    g.FillPath(dimB, cp)
                End Using
            End If
        End Using

        TextRenderer.DrawText(g, _name, Theme.F(12, True), New Rectangle(16, imgH + 12, cw - 32, 26), Theme.TextDark,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        TextRenderer.DrawText(g, If(_hasVariants, "From ", "") & OrderCard.Money(_price), Theme.F(13, True), New Rectangle(16, imgH + 40, cw - 32, 28), Theme.Primary,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, _category, Theme.F(10), New Rectangle(16, imgH + 70, cw - 32, 22), Theme.TextMuted,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)

        If _track Then
            Dim txt As String = If(_stock <= 0, "Sold out", _stock.ToString() & " left")
            Dim pf As Font = Theme.F(9, True)
            Dim pw As Integer = TextRenderer.MeasureText(txt, pf, New Size(300, 40), TextFormatFlags.NoPadding).Width + 22
            Dim pr As New Rectangle(16, imgH + 98, pw, 24)
            Using p As GraphicsPath = Theme.RoundRectPath(pr, 12), b As New SolidBrush(If(_stock <= 0, Theme.DangerSoft, Color.FromArgb(240, 235, 228)))
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, txt, pf, pr, If(_stock <= 0, Theme.Danger, Theme.TextMuted),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End If

        Using pen As New Pen(Color.FromArgb(236, 228, 218), 1)
            g.DrawLine(pen, 16, imgH + 136, cw - 16, imgH + 136)
        End Using
        TextRenderer.DrawText(g, If(_available, "Available", "Unavailable"), Theme.F(11), New Rectangle(16, imgH + 146, cw - 100, 30),
                              If(_available, Theme.TextDark, Theme.TextMuted), TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        _toggleRect = New Rectangle(cw - 16 - 54, imgH + 146, 54, 30)
        SwitchPaint.Draw(g, _toggleRect, _available)

        If _selected Then
            Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(1, 1, cw - 2, Height - 7), 18), pen As New Pen(Theme.Primary, 2.5F)
                g.DrawPath(pen, p)
            End Using
        ElseIf _hover Then
            Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(1, 1, cw - 2, Height - 7), 18), pen As New Pen(Color.FromArgb(110, Theme.Primary), 2.0F)
                g.DrawPath(pen, p)
            End Using
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
        _hover = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If e.Button <> MouseButtons.Left Then Return
        If _toggleRect.Contains(e.Location) Then
            RaiseEvent AvailabilityToggled(Me, EventArgs.Empty)
        Else
            RaiseEvent CardClicked(Me, EventArgs.Empty)
        End If
    End Sub
End Class

''' <summary>Multi-line text field with the same look as AppTextBox.</summary>
Public Class AreaField
    Inherits Control

    Private ReadOnly _tb As New TextBox()

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        _tb.Multiline = True
        _tb.BorderStyle = BorderStyle.None
        _tb.BackColor = Theme.FieldBg
        _tb.ForeColor = Theme.TextDark
        _tb.Font = Theme.F(11)
        _tb.MaxLength = 400
        Controls.Add(_tb)
        AddHandler _tb.GotFocus, Sub(s, e) Invalidate()
        AddHandler _tb.LostFocus, Sub(s, e) Invalidate()
    End Sub

    Public WriteOnly Property MaxLen As Integer
        Set(v As Integer)
            _tb.MaxLength = v
        End Set
    End Property

    Public Property Value As String
        Get
            Return _tb.Text
        End Get
        Set(v As String)
            _tb.Text = If(v, "")
        End Set
    End Property

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        _tb.SetBounds(16, 12, Math.Max(20, Width - 32), Math.Max(20, Height - 24))
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), 14), b As New SolidBrush(Theme.FieldBg), pen As New Pen(If(_tb.Focused, Theme.Primary, Theme.Border), If(_tb.Focused, 1.6F, 1.0F))
            g.FillPath(b, p)
            g.DrawPath(pen, p)
        End Using
    End Sub
End Class

''' <summary>Category field: type a new category or pick one of the stall's existing ones from the dropdown.</summary>
Public Class CategoryField
    Inherits Control

    Public ReadOnly Categories As New List(Of String)
    Private ReadOnly _box As New AppTextBox()
    Private ReadOnly _btn As New IconButton()
    Private _menu As ContextMenuStrip

    Public Sub New()
        SetStyle(ControlStyles.Selectable, False)
        _box.Font = Theme.F(11)
        _box.PlaceholderText = "Pick or type a category"
        _box.MaxLength = 40
        _btn.IconName = "chev_d"
        _btn.IconColor = Theme.TextMuted
        _btn.BorderColor = Theme.Border
        _btn.HoverBack = Theme.PrimarySoft
        _btn.Size = New Size(44, 44)
        AddHandler _btn.Click, AddressOf OnDropdown
        Controls.Add(_box)
        Controls.Add(_btn)
    End Sub

    Public Property Value As String
        Get
            Return _box.Text.Trim()
        End Get
        Set(v As String)
            _box.Text = If(v, "")
        End Set
    End Property

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        _box.SetBounds(0, 0, Math.Max(40, Width - 54), Height)
        _btn.SetBounds(Width - 44, (Height - 44) \ 2, 44, 44)
    End Sub

    Private Sub OnDropdown(sender As Object, e As EventArgs)
        If _menu IsNot Nothing Then _menu.Dispose()
        _menu = New ContextMenuStrip()
        _menu.Font = Theme.F(10.5F)
        If Categories.Count = 0 Then
            Dim none As ToolStripItem = _menu.Items.Add("No categories yet. Type a new one.")
            none.Enabled = False
        Else
            For Each c As String In Categories
                Dim it As ToolStripItem = _menu.Items.Add(c)
                AddHandler it.Click, AddressOf OnPick
            Next
        End If
        _menu.Show(_btn, New Point(0, _btn.Height + 4))
    End Sub

    Private Sub OnPick(sender As Object, e As EventArgs)
        _box.Text = DirectCast(sender, ToolStripItem).Text
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso _menu IsNot Nothing Then _menu.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
