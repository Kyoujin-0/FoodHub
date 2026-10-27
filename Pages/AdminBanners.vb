Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Rounded button painted by hand. Kind 0 = orange (main action), 1 = white with border, 2 = red text (delete).</summary>
Public Class BannerActionButton
    Inherits Control

    Public Property Kind As Integer
    Private _hot As Boolean

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Cursor = Cursors.Hand
        Size = New Size(120, 44)
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        _hot = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _hot = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        Dim back As Color = If(Parent IsNot Nothing, Parent.BackColor, Theme.Bg)
        g.Clear(back)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim r As New Rectangle(0, 0, Width - 1, Height - 1)
        Dim fore As Color
        Using p As GraphicsPath = Theme.RoundRectPath(r, Height \ 2)
            Select Case Kind
                Case 0
                    Using b As New SolidBrush(If(_hot, Theme.PrimaryDark, Theme.Primary))
                        g.FillPath(b, p)
                    End Using
                    fore = Color.White
                Case 1
                    Using b As New SolidBrush(If(_hot, Color.FromArgb(250, 243, 236), Color.White))
                        g.FillPath(b, p)
                    End Using
                    Using pen As New Pen(Theme.Border, 1.0F)
                        g.DrawPath(pen, p)
                    End Using
                    fore = Theme.TextDark
                Case Else
                    Using b As New SolidBrush(If(_hot, Theme.DangerSoft, Color.White))
                        g.FillPath(b, p)
                    End Using
                    Using pen As New Pen(Theme.DangerSoft, 1.0F)
                        g.DrawPath(pen, p)
                    End Using
                    fore = Theme.Danger
            End Select
        End Using
        TextRenderer.DrawText(g, Text, Theme.F(11, True), New Rectangle(0, 0, Width, Height), fore,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix)
    End Sub
End Class

''' <summary>The big preview of the selected banner, exactly as the customer Home draws it.</summary>
Public Class BannerPreviewBox
    Inherits Control

    Private _row As BannerRow
    Private _showSafe As Boolean

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.White
    End Sub

    Public Sub ShowBanner(row As BannerRow)
        _row = row
        Invalidate()
    End Sub

    Public Property ShowSafe As Boolean
        Get
            Return _showSafe
        End Get
        Set(value As Boolean)
            _showSafe = value
            Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim r As New Rectangle(0, 0, Width - 1, Height - 1)
        If _row Is Nothing Then
            Using p As GraphicsPath = Theme.RoundRectPath(r, 22), b As New SolidBrush(Color.FromArgb(250, 243, 236))
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, "No banners yet. Click ""Add banner"" to make the first one.", Theme.F(12), r, Theme.TextMuted,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Return
        End If
        BannerPaint.Draw(g, r, BannerSvc.LoadImage(_row.ImagePath), _row.FocusX, _row.FocusY, _row.Zoom, _row.Headline,
                         _row.Title & _row.Id.ToString(), _row.StallId > 0, _showSafe, 22)
    End Sub
End Class

''' <summary>The list of banners: drag handle, number, thumbnail, title, up/down arrows, Edit. Click selects, handle drags to reorder.</summary>
Public Class BannerList
    Inherits Control

    Public Const RowH As Integer = 100
    Private Const CardH As Integer = 88

    Public Event SelectedChanged(id As Integer)
    Public Event EditRequested(id As Integer)
    Public Event OrderChanged(ids As List(Of Integer))

    Private _rows As New List(Of BannerRow)
    Private _selId As Integer
    Private _dragIdx As Integer = -1
    Private _dropIdx As Integer
    Private _hotKey As Integer = -1

    Private ReadOnly _fTitle As Font = Theme.F(13.5F, True)
    Private ReadOnly _fSub As Font = Theme.F(10.5F)
    Private ReadOnly _fEdit As Font = New Font(Theme.F(11, True), FontStyle.Bold Or FontStyle.Underline)
    Private ReadOnly _fPill As Font = Theme.F(8.5F, True)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then _fEdit.Dispose()
        MyBase.Dispose(disposing)
    End Sub

    Public ReadOnly Property SelectedId As Integer
        Get
            Return _selId
        End Get
    End Property

    Public Sub SetRows(rows As List(Of BannerRow), selId As Integer)
        _rows = rows
        _selId = selId
        If Not rows.Exists(Function(b) b.Id = _selId) Then _selId = If(rows.Count > 0, rows(0).Id, 0)
        _dragIdx = -1
        Invalidate()
    End Sub

    Public Function DesiredHeight() As Integer
        If _rows.Count = 0 Then Return 110
        Return _rows.Count * RowH
    End Function

    ' ---------------- geometry ----------------
    Private Function CardRect(i As Integer) As Rectangle
        Return New Rectangle(0, i * RowH, Width - 1, CardH)
    End Function

    Private Function HandleRect(c As Rectangle) As Rectangle
        Return New Rectangle(c.X + 14, c.Y + (c.Height - 36) \ 2, 30, 36)
    End Function

    Private Function BadgeRect(c As Rectangle) As Rectangle
        Return New Rectangle(c.X + 54, c.Y + (c.Height - 34) \ 2, 34, 34)
    End Function

    Private Function ThumbRect(c As Rectangle) As Rectangle
        Dim w As Integer = 252
        Dim h As Integer = CInt(w / BannerPaint.AspectRatio)
        Return New Rectangle(c.X + 106, c.Y + (c.Height - h) \ 2, w, h)
    End Function

    Private Function UpRect(c As Rectangle) As Rectangle
        Return New Rectangle(c.Right - 150, c.Y + (c.Height - 32) \ 2, 32, 32)
    End Function

    Private Function DownRect(c As Rectangle) As Rectangle
        Return New Rectangle(c.Right - 108, c.Y + (c.Height - 32) \ 2, 32, 32)
    End Function

    Private Function EditRect(c As Rectangle) As Rectangle
        Return New Rectangle(c.Right - 62, c.Y + (c.Height - 28) \ 2, 48, 28)
    End Function

    ''' <summary>Which part of which row is under the point: key = row * 10 + part (1 up, 2 down, 3 edit, 4 handle, 5 rest of the card). -1 = nothing.</summary>
    Private Function HitKey(p As Point) As Integer
        For i As Integer = 0 To _rows.Count - 1
            Dim c As Rectangle = CardRect(i)
            If Not c.Contains(p) Then Continue For
            If UpRect(c).Contains(p) Then Return i * 10 + 1
            If DownRect(c).Contains(p) Then Return i * 10 + 2
            If EditRect(c).Contains(p) Then Return i * 10 + 3
            If HandleRect(c).Contains(p) Then Return i * 10 + 4
            Return i * 10 + 5
        Next
        Return -1
    End Function

    ' ---------------- painting ----------------
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(Theme.Bg)
        g.SmoothingMode = SmoothingMode.AntiAlias
        If _rows.Count = 0 Then
            Dim er As New Rectangle(0, 0, Width - 1, 90)
            Using p As GraphicsPath = Theme.RoundRectPath(er, 24)
                g.FillPath(Brushes.White, p)
            End Using
            TextRenderer.DrawText(g, "Nothing here yet.", Theme.F(11.5F), er, Theme.TextMuted,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Return
        End If

        For i As Integer = 0 To _rows.Count - 1
            PaintRow(g, _rows(i), i)
        Next

        If _dragIdx >= 0 Then
            Dim y As Integer = _dropIdx * RowH - 6
            Using b As New SolidBrush(Theme.Primary), p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, y - 2, Width - 1, 5), 2)
                g.FillPath(b, p)
            End Using
        End If
    End Sub

    Private Sub PaintRow(g As Graphics, b As BannerRow, i As Integer)
        Dim c As Rectangle = CardRect(i)
        Dim selected As Boolean = (b.Id = _selId)
        Dim dim_ As Boolean = Not b.IsActive
        Using p As GraphicsPath = Theme.RoundRectPath(c, 24)
            g.FillPath(Brushes.White, p)
            Using pen As New Pen(If(selected, Theme.Primary, Color.FromArgb(240, 230, 220)), If(selected, 2.0F, 1.0F))
                g.DrawPath(pen, p)
            End Using
        End Using

        ' drag handle (6 dots)
        Dim hr As Rectangle = HandleRect(c)
        Using db As New SolidBrush(If(_hotKey = i * 10 + 4, Theme.Primary, Color.FromArgb(170, 160, 150)))
            For col As Integer = 0 To 1
                For row As Integer = 0 To 2
                    g.FillEllipse(db, hr.X + 8 + col * 9, hr.Y + 6 + row * 9, 4, 4)
                Next
            Next
        End Using

        ' number badge
        Dim br As Rectangle = BadgeRect(c)
        Using bb As New SolidBrush(Theme.PrimarySoft)
            g.FillEllipse(bb, br)
        End Using
        TextRenderer.DrawText(g, (i + 1).ToString(), Theme.F(11, True), br, Theme.Primary,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        ' thumbnail
        Dim tr As Rectangle = ThumbRect(c)
        BannerPaint.Draw(g, tr, BannerSvc.LoadImage(b.ImagePath), b.FocusX, b.FocusY, b.Zoom, "", b.Title & b.Id.ToString(), False, False, 12)
        If dim_ Then
            Using p As GraphicsPath = Theme.RoundRectPath(tr, 12), wb As New SolidBrush(Color.FromArgb(150, 255, 255, 255))
                g.FillPath(wb, p)
            End Using
        End If

        ' texts
        Dim tx As Integer = tr.Right + 22
        Dim up As Rectangle = UpRect(c)
        Dim tw As Integer = Math.Max(60, up.Left - 16 - tx)
        Dim titleColor As Color = If(dim_, Theme.TextMuted, Theme.TextDark)
        Dim title As String = If(String.IsNullOrWhiteSpace(b.Title), "Untitled banner", b.Title)
        Dim titleW As Integer = Math.Min(tw, TextW(title, _fTitle) + 2)
        TextRenderer.DrawText(g, title, _fTitle, New Rectangle(tx, c.Y + 12, titleW, 26), titleColor, TxtLeft)
        If dim_ Then
            Dim pr As New Rectangle(tx + titleW + 8, c.Y + 15, 52, 20)
            If pr.Right <= up.Left - 8 Then DrawPill(g, pr, "Hidden", Color.FromArgb(120, 110, 100), Color.FromArgb(240, 234, 228), _fPill)
        End If
        Dim headline As String = If(String.IsNullOrWhiteSpace(b.Headline), "No headline", "Headline: " & b.Headline)
        TextRenderer.DrawText(g, headline, _fSub, New Rectangle(tx, c.Y + 38, tw, 20), Theme.TextMuted, TxtLeft)
        Dim link As String = If(b.StallId > 0, "Opens " & b.StallName, "No link")
        TextRenderer.DrawText(g, link, _fSub, New Rectangle(tx, c.Y + 58, tw, 20), Theme.TextMuted, TxtLeft)

        ' up / down arrows
        DrawArrow(g, up, True, i > 0, _hotKey = i * 10 + 1)
        DrawArrow(g, DownRect(c), False, i < _rows.Count - 1, _hotKey = i * 10 + 2)

        ' Edit link
        TextRenderer.DrawText(g, "Edit", _fEdit, EditRect(c), If(_hotKey = i * 10 + 3, Theme.PrimaryDark, Theme.TextDark), TxtCenter)
    End Sub

    Private Sub DrawArrow(g As Graphics, r As Rectangle, up As Boolean, enabled As Boolean, hot As Boolean)
        Using b As New SolidBrush(If(hot AndAlso enabled, Theme.PrimarySoft, Color.FromArgb(250, 243, 236)))
            g.FillEllipse(b, r)
        End Using
        Dim cx As Integer = r.X + r.Width \ 2
        Dim cy As Integer = r.Y + r.Height \ 2
        Dim col As Color = If(enabled, Theme.TextDark, Color.FromArgb(200, 190, 180))
        Using pen As New Pen(col, 2.0F)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            pen.LineJoin = LineJoin.Round
            If up Then
                g.DrawLines(pen, New Point() {New Point(cx - 5, cy + 3), New Point(cx, cy - 2), New Point(cx + 5, cy + 3)})
            Else
                g.DrawLines(pen, New Point() {New Point(cx - 5, cy - 3), New Point(cx, cy + 2), New Point(cx + 5, cy - 3)})
            End If
        End Using
    End Sub

    ' ---------------- mouse ----------------
    Private Sub MoveItem(fromIdx As Integer, toIdx As Integer)
        If fromIdx = toIdx OrElse fromIdx < 0 OrElse toIdx < 0 OrElse fromIdx >= _rows.Count OrElse toIdx >= _rows.Count Then Return
        Dim ids As New List(Of Integer)
        For Each b As BannerRow In _rows
            ids.Add(b.Id)
        Next
        Dim moving As Integer = ids(fromIdx)
        ids.RemoveAt(fromIdx)
        ids.Insert(toIdx, moving)
        RaiseEvent OrderChanged(ids)
    End Sub

    Private Sub Select_(id As Integer)
        If id = _selId Then Return
        _selId = id
        Invalidate()
        RaiseEvent SelectedChanged(id)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button <> MouseButtons.Left Then Return
        Dim k As Integer = HitKey(e.Location)
        If k < 0 Then Return
        Dim i As Integer = k \ 10
        Dim part As Integer = k Mod 10
        Dim id As Integer = _rows(i).Id
        Select Case part
            Case 1
                MoveItem(i, i - 1)
            Case 2
                MoveItem(i, i + 1)
            Case 3
                Select_(id)
                RaiseEvent EditRequested(id)
            Case 4
                Select_(id)
                _dragIdx = i
                _dropIdx = i
                Cursor = Cursors.SizeAll
                Invalidate()
            Case Else
                Select_(id)
        End Select
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _dragIdx >= 0 Then
            Dim d As Integer = Math.Max(0, Math.Min(_rows.Count, CInt(Math.Floor(e.Y / CDbl(RowH) + 0.5))))
            If d <> _dropIdx Then
                _dropIdx = d
                Invalidate()
            End If
            Return
        End If
        Dim k As Integer = HitKey(e.Location)
        If k <> _hotKey Then
            _hotKey = k
            Dim part As Integer = If(k < 0, 0, k Mod 10)
            Dim i As Integer = If(k < 0, -1, k \ 10)
            Dim canClick As Boolean = (part = 3) OrElse (part = 1 AndAlso i > 0) OrElse (part = 2 AndAlso i < _rows.Count - 1)
            Cursor = If(canClick, Cursors.Hand, If(part = 4, Cursors.SizeAll, Cursors.Default))
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        If _dragIdx < 0 Then Return
        Dim fromIdx As Integer = _dragIdx
        Dim target As Integer = If(_dropIdx > fromIdx, _dropIdx - 1, _dropIdx)
        _dragIdx = -1
        Cursor = Cursors.Default
        Invalidate()
        MoveItem(fromIdx, target)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hotKey <> -1 AndAlso _dragIdx < 0 Then
            _hotKey = -1
            Cursor = Cursors.Default
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseDoubleClick(e As MouseEventArgs)
        MyBase.OnMouseDoubleClick(e)
        Dim k As Integer = HitKey(e.Location)
        If k >= 0 AndAlso k Mod 10 = 5 Then RaiseEvent EditRequested(_rows(k \ 10).Id)
    End Sub
End Class

''' <summary>Admin Banners tab: preview of the selected banner, the ordered list, and Add / Edit.</summary>
Public Class AdminBannersPage
    Inherits PageBase

    Private ReadOnly _accent As New Panel()
    Private ReadOnly _title As Label
    Private ReadOnly _sub As Label
    Private ReadOnly _add As New BannerActionButton()
    Private ReadOnly _card As New CardPanel()
    Private ReadOnly _preview As New BannerPreviewBox()
    Private ReadOnly _note As Label
    Private ReadOnly _safe As New CheckBox()
    Private ReadOnly _list As New BannerList()
    Private _rows As New List(Of BannerRow)

    Public Sub New()
        _accent.BackColor = Theme.Primary
        _accent.Size = New Size(48, 5)
        _title = Ui.Lbl("Banners", 26, True)
        _sub = Ui.Lbl("Shown on the customer home, in this order. Each banner has its own headline.", 12, False, Theme.TextMuted)

        _add.Text = "Add banner"
        _add.Kind = 0
        _add.Size = New Size(170, 50)

        _note = Ui.Lbl("", 10.5F, False, Theme.TextMuted)
        _note.BackColor = Color.White
        _safe.Text = "Show safe area"
        _safe.AutoSize = True
        _safe.BackColor = Color.White
        _safe.ForeColor = Theme.TextDark
        _safe.Font = Theme.F(10.5F, True)
        _safe.Cursor = Cursors.Hand

        _card.Controls.Add(_preview)
        _card.Controls.Add(_note)
        _card.Controls.Add(_safe)

        Controls.Add(_accent)
        Controls.Add(_title)
        Controls.Add(_sub)
        Controls.Add(_add)
        Controls.Add(_card)
        Controls.Add(_list)

        AddHandler _add.Click, Sub(s, e) OpenEditor(0)
        AddHandler _safe.CheckedChanged, Sub(s, e) _preview.ShowSafe = _safe.Checked
        AddHandler _list.SelectedChanged, Sub(id) ShowSelected()
        AddHandler _list.EditRequested, Sub(id) OpenEditor(id)
        AddHandler _list.OrderChanged, AddressOf OnOrderChanged

        SafeLoad(0)
    End Sub

    Private Function Find(id As Integer) As BannerRow
        For Each b As BannerRow In _rows
            If b.Id = id Then Return b
        Next
        Return Nothing
    End Function

    Private Sub SafeLoad(keepId As Integer)
        Try
            _rows = BannerSvc.GetAll()
        Catch ex As Exception
            _rows = New List(Of BannerRow)
            MessageBox.Show("The banners could not be loaded." & vbCrLf & vbCrLf & ex.Message, "Banners", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
        _list.SetRows(_rows, keepId)
        ShowSelected()
        RelayoutKeepScroll()
    End Sub

    Private Sub ShowSelected()
        Dim b As BannerRow = Find(_list.SelectedId)
        _preview.ShowBanner(b)
        Dim lq As String = ChrW(8220)
        Dim rq As String = ChrW(8221)
        If b Is Nothing Then
            _note.Text = "Add a banner to see how it looks on the customer home."
        Else
            Dim nm As String = If(String.IsNullOrWhiteSpace(b.Title), "Untitled banner", b.Title)
            _note.Text = "Previewing " & lq & nm & rq & ". The left part sits under a cream fade, so keep key content on the right."
        End If
    End Sub

    Private Sub OnOrderChanged(ids As List(Of Integer))
        Dim err As String = ""
        If Not BannerSvc.Reorder(ids, err) Then
            MessageBox.Show("The new order could not be saved." & vbCrLf & vbCrLf & err, "Banners", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
        SafeLoad(_list.SelectedId)
    End Sub

    ''' <summary>Opens the add/edit dialog (id 0 = new banner) and saves what it returns.</summary>
    Private Sub OpenEditor(id As Integer)
        Dim row As BannerRow = Find(id)
        If row Is Nothing Then row = New BannerRow()
        Dim oldPath As String = row.ImagePath
        Dim stalls As List(Of StallChoice)
        Try
            stalls = BannerSvc.GetStalls()
        Catch ex As Exception
            MessageBox.Show("The stalls could not be loaded." & vbCrLf & vbCrLf & ex.Message, "Banners", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End Try

        Using dlg As New BannerEditForm(row, stalls)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            Dim err As String = ""

            If dlg.Deleted Then
                If BannerSvc.Delete(row.Id, err) Then
                    BannerSvc.DeleteImageFile(oldPath)
                    Toast.Show(FindForm(), "Banner deleted")
                    SafeLoad(0)
                Else
                    MessageBox.Show("The banner could not be deleted." & vbCrLf & vbCrLf & err, "Banners", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                End If
                Return
            End If

            Dim result As BannerRow = dlg.Result
            Dim newPath As String = oldPath
            Dim savedNew As Boolean
            Try
                If dlg.NewImageFile <> "" Then
                    newPath = BannerSvc.SaveImage(dlg.NewImageFile)
                    savedNew = True
                ElseIf dlg.PhotoRemoved Then
                    newPath = ""
                End If
            Catch ex As Exception
                MessageBox.Show("The photo could not be saved." & vbCrLf & vbCrLf & ex.Message, "Banners", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End Try
            result.ImagePath = newPath

            If BannerSvc.Save(result, err) Then
                If newPath <> oldPath Then BannerSvc.DeleteImageFile(oldPath)
                Toast.Show(FindForm(), If(row.Id = 0, "Banner added", "Banner saved"))
                Dim keep As Integer = row.Id
                If keep = 0 Then
                    ' a new banner is the last one: select it
                    Dim all As List(Of BannerRow) = BannerSvc.GetAll()
                    If all.Count > 0 Then keep = all(all.Count - 1).Id
                End If
                SafeLoad(keep)
            Else
                If savedNew Then BannerSvc.DeleteImageFile(newPath)
                MessageBox.Show("The banner could not be saved." & vbCrLf & vbCrLf & err, "Banners", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        End Using
    End Sub

    ' ---------------- layout ----------------
    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim right As Integer = M + contentWidth
        _accent.Location = New Point(M, 22)
        _title.Location = New Point(M, 34)
        _sub.Location = New Point(M, 88)
        _add.SetBounds(right - 170, 44, 170, 50)

        Dim top As Integer = 132
        Dim pad As Integer = 22
        Dim pw As Integer = Math.Max(200, contentWidth - 2 * pad)
        Dim ph As Integer = Math.Min(260, CInt(pw / BannerPaint.AspectRatio))
        _preview.SetBounds(pad, pad, pw, ph)
        Dim noteY As Integer = pad + ph + 14
        _note.Location = New Point(pad, noteY + 2)
        _safe.Location = New Point(pad + pw - _safe.PreferredSize.Width, noteY)
        Dim cardH As Integer = noteY + 36
        _card.SetBounds(M, top, contentWidth, cardH)

        Dim listTop As Integer = top + cardH + 24
        Dim listH As Integer = _list.DesiredHeight()
        _list.SetBounds(M, listTop, contentWidth, listH)
        SetContentHeight(listTop + listH + 40)
    End Sub
End Class
