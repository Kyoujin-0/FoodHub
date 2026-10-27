Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Draws the 5-star rows used everywhere in the review screens.</summary>
Public Module StarPaint
    Public ReadOnly EmptyStar As Color = ColorTranslator.FromHtml("#E4DDD3")

    Public Function StarsWidth(size As Integer, gap As Integer) As Integer
        Return 5 * size + 4 * gap
    End Function

    Private Function StarPoints(cx As Single, cy As Single, outer As Single) As PointF()
        Dim pts(9) As PointF
        Dim inner As Single = outer * 0.5F
        For i As Integer = 0 To 9
            Dim ang As Double = (-90 + i * 36) * Math.PI / 180.0
            Dim rad As Single = If(i Mod 2 = 0, outer, inner)
            pts(i) = New PointF(cx + CSng(Math.Cos(ang)) * rad, cy + CSng(Math.Sin(ang)) * rad)
        Next
        Return pts
    End Function

    ''' <summary>Draws five stars, the first <paramref name="filled"/> of them in the star colour.</summary>
    Public Sub DrawStars(g As Graphics, x As Integer, y As Integer, size As Integer, gap As Integer, filled As Integer)
        Dim old As SmoothingMode = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias
        For i As Integer = 1 To 5
            Dim cx As Single = x + (i - 1) * (size + gap) + size / 2.0F
            Dim cy As Single = y + size / 2.0F + 1
            Dim col As Color = If(i <= filled, Theme.Star, EmptyStar)
            Dim pts() As PointF = StarPoints(cx, cy, size / 2.0F)
            Using b As New SolidBrush(col), pen As New Pen(col, 1.4F)
                pen.LineJoin = LineJoin.Round
                g.FillPolygon(b, pts)
                g.DrawPolygon(pen, pts)
            End Using
        Next
        g.SmoothingMode = old
    End Sub
End Module

''' <summary>Five clickable stars (1-5) used when writing a review.</summary>
Public Class StarPicker
    Inherits Control

    Public Event ValueChanged As EventHandler

    Private Const StarSize As Integer = 36
    Private Const StarGap As Integer = 8
    Private _value As Integer
    Private _hover As Integer

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Color.White
        Cursor = Cursors.Hand
        Size = New Size(StarsWidth(StarSize, StarGap), StarSize + 2)
    End Sub

    Public Property Value As Integer
        Get
            Return _value
        End Get
        Set(v As Integer)
            _value = Math.Max(0, Math.Min(5, v))
            Invalidate()
        End Set
    End Property

    Private Function IndexAt(x As Integer) As Integer
        If x < 0 OrElse x >= Width Then Return 0
        Return Math.Min(5, x \ (StarSize + StarGap) + 1)
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim h As Integer = IndexAt(e.X)
        If h <> _hover Then
            _hover = h
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _hover = 0
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        Dim h As Integer = IndexAt(e.X)
        If h > 0 AndAlso h <> _value Then
            _value = h
            Invalidate()
            RaiseEvent ValueChanged(Me, EventArgs.Empty)
        End If
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.Clear(BackColor)
        DrawStars(e.Graphics, 0, 0, StarSize, StarGap, If(_hover > 0, _hover, _value))
    End Sub
End Class

''' <summary>Average rating + a bar per star (like the reference): rounded white card.</summary>
Public Class ReviewSummaryBox
    Inherits Control

    Private _s As New ReviewSummary()
    Private ReadOnly _fBig As Font = Theme.F(34, True)
    Private ReadOnly _fText As Font = Theme.F(11)
    Private ReadOnly _fSmall As Font = Theme.F(10)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Theme.Bg
    End Sub

    Public Sub SetData(s As ReviewSummary)
        _s = s
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), 22), pen As New Pen(Theme.Border)
            g.FillPath(Brushes.White, p)
            g.DrawPath(pen, p)
        End Using

        Dim leftW As Integer = 200
        Dim flags As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter
        Dim avgText As String = If(_s.Total = 0, "0.0", _s.Avg.ToString("0.0"))
        TextRenderer.DrawText(g, avgText, _fBig, New Rectangle(0, 22, leftW, 60), Theme.TextDark, flags)
        Dim stars As Integer = CInt(Math.Floor(_s.Avg + 0.5))
        DrawStars(g, (leftW - StarsWidth(20, 4)) \ 2, 86, 20, 4, stars)
        Dim ratingText As String = _s.Total & If(_s.Total = 1, " rating", " ratings")
        TextRenderer.DrawText(g, ratingText, _fText, New Rectangle(0, 116, leftW, 24), Theme.TextMuted, flags)

        Dim x0 As Integer = leftW + 10
        Dim x1 As Integer = Width - 30
        Dim rowH As Integer = Math.Max(20, (Height - 40) \ 5)
        For star As Integer = 5 To 1 Step -1
            Dim ry As Integer = 20 + (5 - star) * rowH
            TextRenderer.DrawText(g, star.ToString(), _fText, New Rectangle(x0, ry, 18, rowH), Theme.TextMuted,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
            Dim bx As Integer = x0 + 28
            Dim bw As Integer = Math.Max(20, x1 - bx - 44)
            Dim track As New Rectangle(bx, ry + (rowH - 8) \ 2, bw, 8)
            Using tp As GraphicsPath = Theme.RoundRectPath(track, 4), tb As New SolidBrush(EmptyStar)
                g.FillPath(tb, tp)
            End Using
            Dim cnt As Integer = _s.Counts(star)
            If cnt > 0 AndAlso _s.Total > 0 Then
                Dim fw As Integer = Math.Max(8, CInt(bw * cnt / _s.Total))
                Using fp As GraphicsPath = Theme.RoundRectPath(New Rectangle(bx, track.Y, fw, 8), 4), fb As New SolidBrush(Theme.Star)
                    g.FillPath(fb, fp)
                End Using
            End If
            TextRenderer.DrawText(g, cnt.ToString(), _fText, New Rectangle(x1 - 36, ry, 36, rowH), Theme.TextMuted,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter Or TextFormatFlags.Right)
        Next
    End Sub
End Class

''' <summary>Pill-shaped filter tabs with counts (All / Needs reply / Replied).</summary>
Public Class FilterTabs
    Inherits Control

    Public Event SelectedChanged As EventHandler

    Private ReadOnly _names As New List(Of String)
    Private ReadOnly _counts As New List(Of Integer)
    Private ReadOnly _rects As New List(Of Rectangle)
    Private ReadOnly _fName As Font = Theme.F(11.5F, True)
    Private ReadOnly _fCount As Font = Theme.F(9.5F)
    Private _sel As Integer
    Private _hot As Integer = -1

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

    Private Sub LayoutChips()
        _rects.Clear()
        Dim x As Integer = 0
        For i As Integer = 0 To _names.Count - 1
            Dim w As Integer = Measure(_names(i), _fName) + Measure(_counts(i).ToString(), _fCount) + 44
            _rects.Add(New Rectangle(x, 0, w, 44))
            x += w + 10
        Next
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        LayoutChips()
        For i As Integer = 0 To _names.Count - 1
            Dim r As Rectangle = _rects(i)
            Dim isSel As Boolean = (i = _sel)
            Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), 22)
                Using b As New SolidBrush(If(isSel, Theme.Primary, If(i = _hot, Theme.PrimarySoft, Color.White)))
                    g.FillPath(b, p)
                End Using
                If Not isSel Then
                    Using pen As New Pen(Theme.Border)
                        g.DrawPath(pen, p)
                    End Using
                End If
            End Using
            Dim nameW As Integer = Measure(_names(i), _fName)
            TextRenderer.DrawText(g, _names(i), _fName, New Rectangle(r.X + 20, r.Y, nameW + 4, r.Height),
                                  If(isSel, Color.White, Theme.TextDark), TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
            TextRenderer.DrawText(g, _counts(i).ToString(), _fCount, New Rectangle(r.X + 20 + nameW + 8, r.Y + 1, r.Width, r.Height),
                                  If(isSel, Color.FromArgb(255, 235, 224), Theme.TextMuted), TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
        Next
    End Sub

    Private Function HitTest(p As Point) As Integer
        LayoutChips()
        For i As Integer = 0 To _rects.Count - 1
            If _rects(i).Contains(p) Then Return i
        Next
        Return -1
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim h As Integer = HitTest(e.Location)
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
        Dim h As Integer = HitTest(e.Location)
        If h >= 0 AndAlso h <> _sel Then
            _sel = h
            Invalidate()
            RaiseEvent SelectedChanged(Me, EventArgs.Empty)
        End If
    End Sub
End Class

''' <summary>The data one review card shows.</summary>
Public Class ReviewData
    Public Id As Integer
    Public ProductName As String = ""
    Public ImagePath As String = ""
    Public Username As String = ""
    Public Rating As Integer
    Public Comment As String = ""
    Public Created As DateTime
    Public Reply As String = ""
    Public RepliedAt As DateTime
    Public StallName As String = ""
    Public Mine As Boolean

    Public Shared Function FromRow(r As DataRow, mine As Boolean, stallName As String) As ReviewData
        Dim d As New ReviewData()
        d.Id = Db.GetInt(r, "id")
        If r.Table.Columns.Contains("product_name") Then d.ProductName = Db.GetStr(r, "product_name")
        If r.Table.Columns.Contains("image_path") Then d.ImagePath = Db.GetStr(r, "image_path")
        d.Username = Db.GetStr(r, "username")
        d.Rating = Db.GetInt(r, "rating")
        d.Comment = Db.GetStr(r, "comment")
        d.Created = Db.GetDate(r, "created_at")
        d.Reply = Db.GetStr(r, "reply")
        If d.Reply <> "" Then d.RepliedAt = Db.GetDate(r, "replied_at")
        d.StallName = stallName
        d.Mine = mine
        Return d
    End Function
End Class

''' <summary>
''' One review: who, stars, date, comment and the stall's single reply under it.
''' Customer view = plain rows; seller view (Boxed) = white card with the item on top and a reply box when there is no reply yet.
''' </summary>
Public Class ReviewCard
    Inherits Control

    Public ReadOnly Data As ReviewData
    Public Event ReplySubmitted(card As ReviewCard, text As String)
    ''' <summary>The customer clicked Edit on their own review.</summary>
    Public Event EditRequested(card As ReviewCard)

    Private Const Flags As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or TextFormatFlags.WordBreak
    Private ReadOnly _showProduct As Boolean
    Private ReadOnly _canReply As Boolean
    Private ReadOnly _boxed As Boolean
    Private ReadOnly _input As AppTextBox
    Private ReadOnly _send As RoundedButton
    Private ReadOnly _edit As Label
    Private ReadOnly _thumb As Image
    Private ReadOnly _fName As Font = Theme.F(12, True)
    Private ReadOnly _fBody As Font = Theme.F(11.5F)
    Private ReadOnly _fReply As Font = Theme.F(10.5F)
    Private ReadOnly _fSmall As Font = Theme.F(10)
    Private ReadOnly _fTiny As Font = Theme.F(9, True)
    Private _thumbRect As Rectangle
    Private _prodRect As Rectangle
    Private _nameRect As Rectangle
    Private _pillRect As Rectangle
    Private _starsPt As Point
    Private _dateRect As Rectangle
    Private _commentRect As Rectangle
    Private _replyBox As Rectangle
    Private _replyTitle As Rectangle
    Private _replyDate As Rectangle
    Private _replyText As Rectangle
    Private _hintRect As Rectangle

    Public Sub New(d As ReviewData, showProduct As Boolean, canReply As Boolean, boxed As Boolean)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Theme.Bg
        Data = d
        _showProduct = showProduct
        _canReply = canReply AndAlso d.Reply = ""
        _boxed = boxed
        If showProduct Then _thumb = Ui.LoadImage(d.ImagePath)

        _input = New AppTextBox()
        _input.Font = Theme.F(11)
        _input.PlaceholderText = "Write a reply (you can only reply once)"
        _input.MaxLength = 255
        _input.BackColor = Color.White
        _send = Ui.PrimaryBtn("Reply", 114, 46)
        _send.Font = Theme.F(11, True)
        _send.BackColor = Color.White
        AddHandler _send.Click, AddressOf OnSend
        AddHandler _input.EnterPressed, AddressOf OnSend
        _input.Visible = _canReply
        _send.Visible = _canReply
        Controls.Add(_input)
        Controls.Add(_send)
        If d.Mine Then
            _edit = Ui.Link("Edit", 10)
            _edit.BackColor = If(boxed, Color.White, Theme.Bg)
            AddHandler _edit.Click, AddressOf OnEditClick
            Controls.Add(_edit)
        End If
    End Sub

    Private Sub OnEditClick(sender As Object, e As EventArgs)
        RaiseEvent EditRequested(Me)
    End Sub

    ''' <summary>True while the seller has typed a reply that is not sent yet.</summary>
    Public ReadOnly Property HasDraft As Boolean
        Get
            Return _canReply AndAlso _input.Text.Trim() <> ""
        End Get
    End Property

    Private Sub OnSend(sender As Object, e As EventArgs)
        Dim t As String = _input.Text.Trim()
        If t = "" Then
            Ui.Warn("Write a reply first.")
            Return
        End If
        RaiseEvent ReplySubmitted(Me, t)
    End Sub

    Private Shared Function TextH(text As String, f As Font, w As Integer) As Integer
        Return TextRenderer.MeasureText(text, f, New Size(Math.Max(20, w), 10000), Flags).Height
    End Function

    ''' <summary>Sets the width and works out the height (it depends on the comment and the reply).</summary>
    Public Sub ApplyWidth(w As Integer)
        Width = w
        Dim pad As Integer = If(_boxed, 22, 0)
        Dim x0 As Integer = pad
        Dim iw As Integer = Math.Max(160, w - 2 * pad)
        Dim y As Integer = If(_boxed, 20, 14)

        If _showProduct Then
            _thumbRect = New Rectangle(x0, y, 52, 52)
            _prodRect = New Rectangle(x0 + 64, y, Math.Max(40, iw - 64), 52)
            y += 52 + 14
        End If

        Dim nameW As Integer = TextRenderer.MeasureText(Data.Username, _fName, New Size(400, 30), TextFormatFlags.NoPadding).Width
        nameW = Math.Min(nameW, Math.Max(60, iw \ 3))
        _nameRect = New Rectangle(x0, y, nameW + 2, 24)
        Dim x As Integer = x0 + nameW + 12
        If Data.Mine Then
            _pillRect = New Rectangle(x, y + 2, 42, 20)
            x += 42 + 10
        Else
            _pillRect = Rectangle.Empty
        End If
        _starsPt = New Point(x, y + 4)
        If _edit IsNot Nothing Then
            Dim ew As Integer = _edit.PreferredWidth
            _edit.Location = New Point(x0 + iw - ew, y + 3)
            _dateRect = New Rectangle(x0 + iw - ew - 16 - 120, y, 120, 24)
        Else
            _dateRect = New Rectangle(x0 + iw - 120, y, 120, 24)
        End If
        y += 30

        If Data.Comment <> "" Then
            Dim h As Integer = TextH(Data.Comment, _fBody, iw)
            _commentRect = New Rectangle(x0, y, iw, h)
            y += h + 10
        Else
            _commentRect = Rectangle.Empty
        End If

        If Data.Reply <> "" Then
            Dim bx As Integer = x0 + 18
            Dim bw As Integer = iw - 18
            Dim th As Integer = TextH(Data.Reply, _fReply, bw - 32)
            _replyBox = New Rectangle(bx, y, bw, 14 + 20 + 6 + th + 16)
            _replyTitle = New Rectangle(bx + 16, y + 14, Math.Max(40, bw - 32 - 110), 20)
            _replyDate = New Rectangle(bx + bw - 16 - 110, y + 14, 110, 20)
            _replyText = New Rectangle(bx + 16, y + 14 + 20 + 6, bw - 32, th)
            y += _replyBox.Height + 10
        Else
            _replyBox = Rectangle.Empty
        End If

        If _canReply Then
            _input.SetBounds(x0, y, Math.Max(80, iw - 124), 46)
            _send.SetBounds(x0 + iw - 114, y, 114, 46)
            _hintRect = New Rectangle(x0 + 4, y + 52, iw, 18)
            y += 52 + 24
        Else
            _hintRect = Rectangle.Empty
        End If

        y += If(_boxed, 12, 8)
        Height = y
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim vc As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or TextFormatFlags.VerticalCenter

        If _boxed Then
            Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), 20), pen As New Pen(Theme.Border)
                g.FillPath(Brushes.White, p)
                g.DrawPath(pen, p)
            End Using
        Else
            Using pen As New Pen(Theme.Border)
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1)
            End Using
        End If

        If _showProduct Then
            Using tp As GraphicsPath = Theme.RoundRectPath(_thumbRect, 12)
                If _thumb Is Nothing Then
                    Using tb As New SolidBrush(Theme.PrimarySoft)
                        g.FillPath(tb, tp)
                    End Using
                    Dim initial As String = If(Data.ProductName.Length > 0, Data.ProductName.Substring(0, 1).ToUpper(), "?")
                    TextRenderer.DrawText(g, initial, _fName, _thumbRect, Theme.Primary,
                                          TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                Else
                    Dim state As GraphicsState = g.Save()
                    g.SetClip(tp)
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    Dim s As Integer = Math.Min(_thumb.Width, _thumb.Height)
                    Dim src As New Rectangle((_thumb.Width - s) \ 2, (_thumb.Height - s) \ 2, s, s)
                    g.DrawImage(_thumb, _thumbRect, src, GraphicsUnit.Pixel)
                    g.Restore(state)
                End If
            End Using
            TextRenderer.DrawText(g, Data.ProductName, _fName, _prodRect, Theme.TextDark, vc Or TextFormatFlags.EndEllipsis)
        End If

        TextRenderer.DrawText(g, Data.Username, _fName, _nameRect, Theme.TextDark, vc Or TextFormatFlags.EndEllipsis)
        If Data.Mine Then
            Using pp As GraphicsPath = Theme.RoundRectPath(_pillRect, 10), pb As New SolidBrush(Theme.PrimarySoft)
                g.FillPath(pb, pp)
            End Using
            TextRenderer.DrawText(g, "You", _fTiny, _pillRect, Theme.Primary,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
        End If
        DrawStars(g, _starsPt.X, _starsPt.Y, 16, 3, Data.Rating)
        TextRenderer.DrawText(g, Data.Created.ToString("MMM d, yyyy"), _fSmall, _dateRect, Theme.TextMuted, vc Or TextFormatFlags.Right)

        If Not _commentRect.IsEmpty Then
            TextRenderer.DrawText(g, Data.Comment, _fBody, _commentRect, Theme.TextDark, Flags Or TextFormatFlags.Left Or TextFormatFlags.Top)
        End If

        If Not _replyBox.IsEmpty Then
            Using rp As GraphicsPath = Theme.RoundRectPath(_replyBox, 14), rb As New SolidBrush(Theme.PrimarySoft)
                g.FillPath(rb, rp)
            End Using
            TextRenderer.DrawText(g, "Reply from " & Data.StallName, _fSmall, _replyTitle, Theme.Primary, vc Or TextFormatFlags.EndEllipsis)
            TextRenderer.DrawText(g, Data.RepliedAt.ToString("MMM d, yyyy"), _fSmall, _replyDate, Theme.TextMuted, vc Or TextFormatFlags.Right)
            TextRenderer.DrawText(g, Data.Reply, _fReply, _replyText, Theme.TextDark, Flags Or TextFormatFlags.Left Or TextFormatFlags.Top)
        End If

        If Not _hintRect.IsEmpty Then
            TextRenderer.DrawText(g, "One reply per review. It can't be edited or followed up afterwards.", _fSmall, _hintRect, Theme.TextMuted,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or TextFormatFlags.VerticalCenter)
        End If
    End Sub
End Class

''' <summary>The "Reviews" block of the product page: heading, write button (only after a completed order), rating summary and the review list.</summary>
Public Class ReviewsSection
    Inherits Control

    ''' <summary>Raised after the customer saved or deleted a review (new average and count).</summary>
    Public Event DataChanged(avg As Double, total As Integer)
    ''' <summary>Raised when the section's height changed, so the page must lay out again.</summary>
    Public Event HeightChanged As EventHandler

    Private ReadOnly _productId As Integer
    Private ReadOnly _productName As String
    Private ReadOnly _stallName As String
    Private ReadOnly _title As Label
    Private ReadOnly _write As RoundedButton
    Private ReadOnly _info As Label
    Private ReadOnly _box As New ReviewSummaryBox()
    Private ReadOnly _note As Label
    Private ReadOnly _cards As New List(Of ReviewCard)
    Private _reviewableOrder As Integer
    Private _hasMine As Boolean
    Private _failed As Boolean

    Public Sub New(productId As Integer, productName As String, stallName As String)
        SetStyle(ControlStyles.Selectable, False)
        BackColor = Theme.Bg
        _productId = productId
        _productName = productName
        _stallName = stallName

        _title = Ui.Lbl("Reviews", 18, True)
        _write = Ui.OutlineBtn("Write a review", 180, 46)
        _write.Radius = 23
        _write.Font = Theme.F(11, True)
        _write.BackColor = Theme.Bg
        _write.Visible = False
        AddHandler _write.Click, AddressOf OnWrite
        _info = Ui.Lbl("", 10, False, Theme.TextMuted)
        _note = Ui.Lbl("", 10.5F, False, Theme.TextMuted)
        Controls.Add(_title)
        Controls.Add(_write)
        Controls.Add(_info)
        Controls.Add(_box)
        Controls.Add(_note)
        LoadData()
    End Sub

    Private Sub ClearCards()
        For Each c As ReviewCard In _cards
            Controls.Remove(c)
            c.Dispose()
        Next
        _cards.Clear()
    End Sub

    Private Sub LoadData()
        ClearCards()
        _hasMine = False
        _reviewableOrder = 0
        _failed = False
        Dim isCustomer As Boolean = (Session.Role = "customer")
        Try
            _box.SetData(ReviewSvc.GetSummary(_productId))
            Dim dt As DataTable = ReviewSvc.GetReviews(_productId)
            For Each r As DataRow In dt.Rows
                Dim mine As Boolean = (isCustomer AndAlso Db.GetInt(r, "customer_id") = Session.UserId)
                Dim card As New ReviewCard(ReviewData.FromRow(r, mine, _stallName), False, False, False)
                If mine Then
                    _hasMine = True
                    AddHandler card.EditRequested, AddressOf OnEdit
                End If
                _cards.Add(card)
                Controls.Add(card)
            Next
            _note.Text = If(dt.Rows.Count = 0, "No reviews yet.", "")
            _reviewableOrder = ReviewSvc.GetReviewableOrder(_productId)
        Catch ex As Exception
            _failed = True
            _box.SetData(New ReviewSummary())
            _note.Text = "Reviews could not be loaded. Run patch_reviews.sql on your database."
        End Try
        _write.Visible = isCustomer AndAlso Not _failed AndAlso _reviewableOrder > 0
        If isCustomer AndAlso Not _failed AndAlso _reviewableOrder = 0 Then
            _info.Text = If(_hasMine, "You can review again after your next completed order.", "You can review this item after a completed order.")
        Else
            _info.Text = ""
        End If
    End Sub

    ''' <summary>Positions everything for the given width and returns the section height.</summary>
    Public Function LayoutFor(w As Integer) As Integer
        Dim y As Integer = 30
        _title.Location = New Point(0, y + 4)
        _write.Location = New Point(w - _write.Width, y - 6)
        _info.Visible = (_info.Text <> "")
        If _info.Visible Then _info.Location = New Point(Math.Max(110, w - _info.PreferredWidth), y + 8)
        y += 56
        _box.SetBounds(0, y, w, 172)
        y += 172 + 20
        For Each c As ReviewCard In _cards
            c.ApplyWidth(w)
            c.SetBounds(0, y, w, c.Height)
            y += c.Height
        Next
        _note.Visible = (_note.Text <> "")
        If _note.Visible Then
            _note.Location = New Point(4, y + 4)
            y += 34
        End If
        Return y + 24
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Using pen As New Pen(Theme.Border)
            e.Graphics.DrawLine(pen, 0, 0, Width, 0)
        End Using
    End Sub

    Private Sub OnWrite(sender As Object, e As EventArgs)
        Using f As New ReviewForm(_productName, 0, "", False)
            If f.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            Try
                Dim err As String = ""
                If Not ReviewSvc.AddReview(_productId, _reviewableOrder, f.Rating, f.Comment, err) Then
                    Ui.Warn(err)
                    BeginInvoke(New Action(Of String)(AddressOf AfterChange), "")
                    Return
                End If
            Catch ex As Exception
                Ui.Warn("Could not save your review: " & ex.Message)
                Return
            End Try
        End Using
        BeginInvoke(New Action(Of String)(AddressOf AfterChange), "Thanks! Your review was saved")
    End Sub

    Private Sub OnEdit(card As ReviewCard)
        Dim msg As String = ""
        Using f As New ReviewForm(_productName, card.Data.Rating, card.Data.Comment, True)
            If f.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            Try
                If f.Deleted Then
                    ReviewSvc.DeleteReview(card.Data.Id)
                    msg = "Your review was deleted"
                Else
                    Dim err As String = ""
                    If Not ReviewSvc.UpdateReview(card.Data.Id, f.Rating, f.Comment, err) Then
                        Ui.Warn(err)
                        Return
                    End If
                    msg = "Your review was updated"
                End If
            Catch ex As Exception
                Ui.Warn("Could not save your review: " & ex.Message)
                Return
            End Try
        End Using
        ' the card that raised the event is rebuilt, so wait until its click has finished
        BeginInvoke(New Action(Of String)(AddressOf AfterChange), msg)
    End Sub

    Private Sub AfterChange(msg As String)
        LoadData()
        Try
            Dim s As ReviewSummary = ReviewSvc.GetSummary(_productId)
            RaiseEvent DataChanged(s.Avg, s.Total)
        Catch
        End Try
        RaiseEvent HeightChanged(Me, EventArgs.Empty)
        If msg <> "" Then Toast.Show(FindForm(), msg)
    End Sub
End Class
