Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Small helpers shared by the seller Inbox and the customer MessageForm.</summary>
Public Module InboxUi
    Public ReadOnly ChatBg As Color = Color.FromArgb(255, 253, 251)

    Public Function AvatarColor(name As String) As Color
        Dim h As Long = 7
        For Each ch As Char In If(name, "")
            h = (h * 31L + AscW(ch)) Mod 1000003L
        Next
        Dim palette() As Color = {Color.FromArgb(255, 107, 53), Color.FromArgb(46, 160, 120), Color.FromArgb(79, 125, 214),
                                  Color.FromArgb(176, 92, 190), Color.FromArgb(214, 148, 32), Color.FromArgb(200, 80, 100)}
        Return palette(CInt(h Mod palette.Length))
    End Function

    Public Sub DrawAvatar(g As Graphics, r As Rectangle, name As String)
        Using b As New SolidBrush(AvatarColor(name))
            g.FillEllipse(b, r)
        End Using
        Dim initial As String = If(If(name, "") <> "", name.Substring(0, 1).ToUpper(), "?")
        TextRenderer.DrawText(g, initial, Theme.F(CSng(r.Height * 0.38), True), r, Color.White,
                              TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
    End Sub

    Public Function DateText(d As DateTime) As String
        If d.Date = DateTime.Today Then Return d.ToString("h:mm tt")
        If d.Date = DateTime.Today.AddDays(-1) Then Return "Yesterday"
        Return d.ToString("MMM d")
    End Function

    Public Function DateChip(d As DateTime) As String
        If d.Date = DateTime.Today Then Return "Today"
        If d.Date = DateTime.Today.AddDays(-1) Then Return "Yesterday"
        If d.Year <> DateTime.Today.Year Then Return d.ToString("MMM d, yyyy")
        Return d.ToString("MMM d")
    End Function

    Public Function StatusText(status As String) As String
        Select Case status
            Case "pending" : Return "Placed"
            Case "preparing" : Return "Preparing"
            Case "ready" : Return "Ready"
            Case "completed" : Return "Completed"
            Case "cancelled" : Return "Declined"
        End Select
        Return status
    End Function

    Public Function IsFinished(status As String) As Boolean
        Return status = "completed" OrElse status = "cancelled"
    End Function

    Public Function LockText(status As String) As String
        If status = "cancelled" Then Return "This order was declined. You can't send new messages."
        Return "This order is finished. You can't send new messages."
    End Function

    Public Sub PillColors(status As String, ByRef fill As Color, ByRef fore As Color)
        Select Case status
            Case "pending", "preparing"
                fill = Color.FromArgb(255, 240, 214)
                fore = Color.FromArgb(160, 100, 10)
            Case "ready"
                fill = Theme.SuccessSoft
                fore = Theme.Success
            Case "cancelled"
                fill = Theme.DangerSoft
                fore = Theme.Danger
            Case Else
                fill = Color.FromArgb(240, 233, 224)
                fore = Theme.TextMuted
        End Select
    End Sub

    ''' <summary>Shows the picked photo for confirmation, then stores and sends it. True when it was sent.</summary>
    Public Function SendPickedImage(owner As Control, orderStallId As Integer, filePath As String) As Boolean
        Dim img As Image = InboxSvc.LoadImgFile(filePath)
        If img Is Nothing Then
            Ui.Warn("That file could not be read as an image.")
            Return False
        End If
        Dim ok As Boolean = False
        Using f As New ImageViewForm(img, True)
            ok = (f.ShowDialog(owner.FindForm()) = DialogResult.OK)
        End Using
        If Not ok Then Return False
        Dim err As String = ""
        Dim rel As String = InboxSvc.SaveImage(filePath, err)
        If rel = "" Then
            Ui.Warn(err)
            Return False
        End If
        If Not InboxSvc.Send(orderStallId, "", rel, err) Then
            InboxSvc.DeleteImageFile(rel)
            Ui.Warn(err)
            Return False
        End If
        Return True
    End Function

    Public Sub ViewPhoto(owner As Control, storedPath As String)
        Dim img As Image = InboxSvc.LoadImg(storedPath)
        If img Is Nothing Then
            Ui.Info("This photo is no longer available.")
            Return
        End If
        Using f As New ImageViewForm(img, False)
            f.ShowDialog(owner.FindForm())
        End Using
    End Sub
End Module

''' <summary>Lets a custom control scroll with the mouse wheel when the cursor is over it (without needing focus).</summary>
Friend Class WheelHook
    Implements IMessageFilter

    Private ReadOnly _c As Control
    Private ReadOnly _onWheel As Action(Of Integer)

    Public Sub New(c As Control, onWheel As Action(Of Integer))
        _c = c
        _onWheel = onWheel
        Application.AddMessageFilter(Me)
    End Sub

    Public Sub Detach()
        Application.RemoveMessageFilter(Me)
    End Sub

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        If m.Msg <> &H20A Then Return False
        If _c.IsDisposed OrElse Not _c.IsHandleCreated OrElse Not _c.Visible Then Return False
        Dim f As Form = _c.FindForm()
        If f Is Nothing OrElse Not f.Equals(Form.ActiveForm) Then Return False
        If Not _c.RectangleToScreen(_c.ClientRectangle).Contains(Control.MousePosition) Then Return False
        Dim hi As Integer = CInt((m.WParam.ToInt64() >> 16) And &HFFFFL)
        If hi > 32767 Then hi -= 65536
        _onWheel(hi)
        Return True
    End Function
End Class

''' <summary>The message bubbles of one conversation (custom painted, scrolls, shows photos).</summary>
Public Class ChatView
    Inherits Control

    Public Event ImageClicked(storedPath As String)

    Private Class Item
        Public Kind As Integer ' 0 = date chip, 1 = bubble
        Public Rect As Rectangle
        Public Text As String = ""
        Public Mine As Boolean
        Public TextRect As Rectangle
        Public TimeText As String = ""
        Public TimeRect As Rectangle
        Public ImgPath As String = ""
        Public ImgRect As Rectangle
        Public Img As Image
    End Class

    Private ReadOnly _items As New List(Of Item)
    Private ReadOnly _imgs As New Dictionary(Of String, Image)
    Private ReadOnly _fBody As Font = Theme.F(11.5F)
    Private ReadOnly _fTime As Font = Theme.F(9)
    Private ReadOnly _fChip As Font = Theme.F(9.5F, True)
    Private ReadOnly _fEmpty As Font = Theme.F(11)
    Private ReadOnly _hook As WheelHook
    Private Shared ReadOnly BodyFlags As TextFormatFlags = TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix Or TextFormatFlags.NoPadding Or TextFormatFlags.Left Or TextFormatFlags.Top
    Private _data As DataTable
    Private _me As Integer
    Private _scroll As Integer
    Private _contentH As Integer
    Private _laidOutW As Integer = -1

    Public Property EmptyText As String = "No messages yet."

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = InboxUi.ChatBg
        _hook = New WheelHook(Me, AddressOf WheelScroll)
    End Sub

    Public Sub Reset()
        _data = Nothing
        _items.Clear()
        _scroll = 0
        _contentH = 0
        Invalidate()
    End Sub

    Public Sub SetMessages(dt As DataTable, myUserId As Integer)
        Dim first As Boolean = (_data Is Nothing)
        Dim atBottom As Boolean = (_scroll >= MaxScroll() - 4)
        _data = dt
        _me = myUserId
        Rebuild()
        If first OrElse atBottom Then
            _scroll = MaxScroll()
        Else
            _scroll = Math.Min(_scroll, MaxScroll())
        End If
        Invalidate()
    End Sub

    Public Sub ScrollToBottom()
        _scroll = MaxScroll()
        Invalidate()
    End Sub

    Private Function MaxScroll() As Integer
        Return Math.Max(0, _contentH - Height)
    End Function

    Private Sub WheelScroll(delta As Integer)
        _scroll = Math.Max(0, Math.Min(MaxScroll(), _scroll - CInt(delta / 120.0 * 70)))
        Invalidate()
    End Sub

    Private Function GetImg(path1 As String) As Image
        If _imgs.ContainsKey(path1) Then Return _imgs(path1)
        Dim img As Image = InboxSvc.LoadImg(path1)
        _imgs(path1) = img
        Return img
    End Function

    Private Sub Rebuild()
        _items.Clear()
        _laidOutW = Width
        Dim w As Integer = Width
        Dim padX As Integer = 28
        Dim y As Integer = 18
        If _data Is Nothing OrElse w < 100 Then
            _contentH = 0
            Return
        End If
        Dim maxTextW As Integer = Math.Max(160, Math.Min(520, CInt((w - padX * 2) * 0.6)))
        Dim lastDate As DateTime = DateTime.MinValue

        For Each r As DataRow In _data.Rows
            Dim sent As DateTime = Db.GetDate(r, "sent_at")
            If sent.Date <> lastDate Then
                lastDate = sent.Date
                Dim label As String = InboxUi.DateChip(sent)
                Dim csz As Size = TextRenderer.MeasureText(label, _fChip, New Size(400, 40), TextFormatFlags.NoPadding)
                Dim cw As Integer = csz.Width + 28
                Dim chip As New Item()
                chip.Kind = 0
                chip.Text = label
                chip.Rect = New Rectangle((w - cw) \ 2, y, cw, 26)
                _items.Add(chip)
                y += 26 + 14
            End If

            Dim it As New Item()
            it.Kind = 1
            it.Mine = (Db.GetInt(r, "sender_id") = _me)
            it.Text = Db.GetStr(r, "body")
            it.ImgPath = Db.GetStr(r, "image_path")
            it.TimeText = sent.ToString("h:mm tt")

            Dim imgW As Integer = 0
            Dim imgH As Integer = 0
            If it.ImgPath <> "" Then
                it.Img = GetImg(it.ImgPath)
                If it.Img IsNot Nothing Then
                    Dim sc As Double = Math.Min(1.0, Math.Min(300.0 / it.Img.Width, 300.0 / it.Img.Height))
                    imgW = Math.Max(1, CInt(it.Img.Width * sc))
                    imgH = Math.Max(1, CInt(it.Img.Height * sc))
                Else
                    imgW = 220
                    imgH = 90
                End If
            End If
            Dim txtSz As Size = Size.Empty
            If it.Text <> "" Then txtSz = TextRenderer.MeasureText(it.Text, _fBody, New Size(maxTextW, 0), BodyFlags)
            Dim timeSz As Size = TextRenderer.MeasureText(it.TimeText, _fTime, New Size(200, 30), TextFormatFlags.NoPadding)

            Dim padH As Integer = If(it.Text = "", 6, 16)
            Dim padV As Integer = If(it.Text = "", 6, 10)
            Dim contentW As Integer = Math.Max(imgW, Math.Max(If(it.Text <> "", txtSz.Width + 4, 0), timeSz.Width))
            Dim bw As Integer = contentW + padH * 2
            Dim bh As Integer = padV + imgH + If(imgH > 0 AndAlso it.Text <> "", 6, 0) + If(it.Text <> "", txtSz.Height + 2, 0) + 4 + timeSz.Height + padV
            Dim bx As Integer = If(it.Mine, w - padX - 8 - bw, padX)
            it.Rect = New Rectangle(bx, y, bw, bh)
            Dim cy As Integer = y + padV
            If imgH > 0 Then
                it.ImgRect = New Rectangle(bx + padH, cy, imgW, imgH)
                cy += imgH + If(it.Text <> "", 6, 0)
            End If
            If it.Text <> "" Then
                it.TextRect = New Rectangle(bx + padH, cy, txtSz.Width + 4, txtSz.Height + 2)
            End If
            it.TimeRect = New Rectangle(bx + bw - padH - timeSz.Width, y + bh - padV - timeSz.Height, timeSz.Width + 2, timeSz.Height)
            _items.Add(it)
            y += bh + 10
        Next
        _contentH = y + 12
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If _data IsNot Nothing AndAlso Width <> _laidOutW Then
            Dim atBottom As Boolean = (_scroll >= MaxScroll() - 4)
            Rebuild()
            If atBottom Then _scroll = MaxScroll() Else _scroll = Math.Min(_scroll, MaxScroll())
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(InboxUi.ChatBg)
        g.SmoothingMode = SmoothingMode.AntiAlias
        If _items.Count = 0 Then
            TextRenderer.DrawText(g, EmptyText, _fEmpty, New Rectangle(40, Height \ 2 - 40, Math.Max(50, Width - 80), 80), Theme.TextMuted,
                                  TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            Return
        End If
        For Each it As Item In _items
            Dim r As Rectangle = it.Rect
            r.Offset(0, -_scroll)
            If r.Bottom < 0 OrElse r.Top > Height Then Continue For
            If it.Kind = 0 Then
                Using p As GraphicsPath = Theme.RoundRectPath(r, 13), b As New SolidBrush(Color.FromArgb(242, 236, 229))
                    g.FillPath(b, p)
                End Using
                TextRenderer.DrawText(g, it.Text, _fChip, r, Theme.TextMuted, TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                Continue For
            End If

            Using p As GraphicsPath = Theme.RoundRectPath(r, 18)
                If it.Mine Then
                    Using b As New SolidBrush(Theme.Primary)
                        g.FillPath(b, p)
                    End Using
                Else
                    g.FillPath(Brushes.White, p)
                    Using pen As New Pen(Theme.Border, 1.0F)
                        g.DrawPath(pen, p)
                    End Using
                End If
            End Using

            If it.ImgPath <> "" Then
                Dim ir As Rectangle = it.ImgRect
                ir.Offset(0, -_scroll)
                If it.Img IsNot Nothing Then
                    Dim st As GraphicsState = g.Save()
                    Using cp As GraphicsPath = Theme.RoundRectPath(ir, 12)
                        g.SetClip(cp)
                    End Using
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    g.DrawImage(it.Img, ir)
                    g.Restore(st)
                Else
                    Using cp As GraphicsPath = Theme.RoundRectPath(ir, 12), b As New SolidBrush(Color.FromArgb(240, 233, 224))
                        g.FillPath(b, cp)
                    End Using
                    TextRenderer.DrawText(g, "Photo unavailable", _fTime, ir, Theme.TextMuted, TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                End If
            End If

            If it.Text <> "" Then
                Dim tr As Rectangle = it.TextRect
                tr.Offset(0, -_scroll)
                TextRenderer.DrawText(g, it.Text, _fBody, tr, If(it.Mine, Color.White, Theme.TextDark), BodyFlags)
            End If
            Dim tmr As Rectangle = it.TimeRect
            tmr.Offset(0, -_scroll)
            TextRenderer.DrawText(g, it.TimeText, _fTime, tmr, If(it.Mine, Color.FromArgb(255, 232, 220), Theme.TextMuted), TextFormatFlags.NoPadding)
        Next

        If _contentH > Height Then
            Dim th As Integer = Math.Max(30, CInt(Height * CDbl(Height) / _contentH))
            Dim ty As Integer = CInt((Height - th) * (CDbl(_scroll) / Math.Max(1, MaxScroll())))
            Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(Width - 7, ty, 4, th), 2), b As New SolidBrush(Color.FromArgb(70, 120, 90, 60))
                g.FillPath(b, p)
            End Using
        End If
    End Sub

    Private Function ImageAt(pt As Point) As Item
        For Each it As Item In _items
            If it.Kind = 1 AndAlso it.ImgPath <> "" Then
                Dim ir As Rectangle = it.ImgRect
                ir.Offset(0, -_scroll)
                If ir.Contains(pt) Then Return it
            End If
        Next
        Return Nothing
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim hit As Item = ImageAt(e.Location)
        Cursor = If(hit IsNot Nothing AndAlso hit.Img IsNot Nothing, Cursors.Hand, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        Dim hit As Item = ImageAt(e.Location)
        If hit IsNot Nothing AndAlso hit.Img IsNot Nothing Then RaiseEvent ImageClicked(hit.ImgPath)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If _hook IsNot Nothing Then _hook.Detach()
            For Each kv As KeyValuePair(Of String, Image) In _imgs
                If kv.Value IsNot Nothing Then kv.Value.Dispose()
            Next
            _imgs.Clear()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>Left column of the seller Inbox: one row per conversation.</summary>
Public Class ConvoList
    Inherits Control

    Public Event ConvoClicked(orderStallId As Integer)

    Private Const RowH As Integer = 108
    Private ReadOnly _fName As Font = Theme.F(13.5F, True)
    Private ReadOnly _fSub As Font = Theme.F(10.5F)
    Private ReadOnly _fMsg As Font = Theme.F(11.5F)
    Private ReadOnly _fMsgBold As Font = Theme.F(11.5F, True)
    Private ReadOnly _fSmall As Font = Theme.F(10)
    Private ReadOnly _hook As WheelHook
    Private _dt As DataTable
    Private _scroll As Integer
    Private _hot As Integer = -1
    Private _selected As Integer

    Public Property EmptyText As String = "No conversations yet."
    Public Property ShowExpiry As Boolean

    Public Property SelectedId As Integer
        Get
            Return _selected
        End Get
        Set(value As Integer)
            _selected = value
            Invalidate()
        End Set
    End Property

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.White
        _hook = New WheelHook(Me, AddressOf WheelScroll)
    End Sub

    Public Sub SetData(dt As DataTable)
        _dt = dt
        _scroll = Math.Min(_scroll, MaxScroll())
        Invalidate()
    End Sub

    Private Function Count() As Integer
        Return If(_dt Is Nothing, 0, _dt.Rows.Count)
    End Function

    Private Function MaxScroll() As Integer
        Return Math.Max(0, Count() * RowH - Height)
    End Function

    Private Sub WheelScroll(delta As Integer)
        _scroll = Math.Max(0, Math.Min(MaxScroll(), _scroll - CInt(delta / 120.0 * 70)))
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(Color.White)
        g.SmoothingMode = SmoothingMode.AntiAlias
        If Count() = 0 Then
            TextRenderer.DrawText(g, EmptyText, _fSub, New Rectangle(30, 40, Math.Max(50, Width - 60), 120), Theme.TextMuted,
                                  TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix Or TextFormatFlags.HorizontalCenter)
            Return
        End If
        For i As Integer = 0 To _dt.Rows.Count - 1
            Dim y As Integer = i * RowH - _scroll
            If y + RowH < 0 OrElse y > Height Then Continue For
            Dim r As DataRow = _dt.Rows(i)
            Dim id As Integer = Db.GetInt(r, "id")
            Dim name As String = Db.GetStr(r, "username")
            Dim status As String = Db.GetStr(r, "status")
            Dim unread As Integer = Db.GetInt(r, "unread")

            If id = _selected Then
                Using b As New SolidBrush(Theme.PrimarySoft)
                    g.FillRectangle(b, 0, y, Width, RowH)
                End Using
                Using b As New SolidBrush(Theme.Primary)
                    g.FillRectangle(b, 0, y, 3, RowH)
                End Using
            ElseIf i = _hot Then
                Using b As New SolidBrush(Color.FromArgb(252, 248, 244))
                    g.FillRectangle(b, 0, y, Width, RowH)
                End Using
            End If
            Using pen As New Pen(Theme.Border, 1.0F)
                g.DrawLine(pen, 0, y + RowH - 1, Width, y + RowH - 1)
            End Using

            InboxUi.DrawAvatar(g, New Rectangle(24, y + (RowH - 56) \ 2, 56, 56), name)

            Dim tx As Integer = 96
            Dim rightW As Integer = 78
            Dim flags As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.VerticalCenter
            TextRenderer.DrawText(g, name, _fName, New Rectangle(tx, y + 14, Width - tx - rightW - 20, 26), Theme.TextDark, flags)
            TextRenderer.DrawText(g, InboxUi.DateText(Db.GetDate(r, "sent_at")), _fSmall, New Rectangle(Width - 24 - rightW, y + 16, rightW, 22), Theme.TextMuted,
                                  flags Or TextFormatFlags.Right)
            TextRenderer.DrawText(g, "Order #" & Db.GetInt(r, "order_id").ToString() & " " & ChrW(&HB7) & " " & InboxUi.StatusText(status), _fSub,
                                  New Rectangle(tx, y + 42, Width - tx - 24, 22), Theme.TextMuted, flags)

            Dim preview As String
            If Db.GetStr(r, "body") = "" AndAlso Db.GetStr(r, "image_path") <> "" Then
                preview = "Photo"
            Else
                preview = Db.GetStr(r, "body").Replace(vbCr, " ").Replace(vbLf, " ")
            End If
            If Db.GetInt(r, "sender_id") = Session.UserId Then preview = "You: " & preview
            TextRenderer.DrawText(g, preview, If(unread > 0, _fMsgBold, _fMsg), New Rectangle(tx, y + 66, Width - tx - 24 - If(unread > 0, 30, 0), 26), Theme.TextDark, flags)

            If unread > 0 Then
                Dim br As New Rectangle(Width - 24 - 22, y + 68, 22, 22)
                Using b As New SolidBrush(Theme.Primary)
                    g.FillEllipse(b, br)
                End Using
                TextRenderer.DrawText(g, If(unread > 9, "9+", unread.ToString()), Theme.F(8.5F, True), br, Color.White,
                                      TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            End If

            If ShowExpiry Then
                Dim left As Integer = InboxSvc.KeepDays - CInt((DateTime.Today - Db.GetDate(r, "done_at").Date).TotalDays)
                left = Math.Max(0, left)
                TextRenderer.DrawText(g, left.ToString() & "d left", Theme.F(9), New Rectangle(Width - 24 - rightW, y + 44, rightW, 20), Theme.Danger,
                                      TextFormatFlags.NoPadding Or TextFormatFlags.Right Or TextFormatFlags.VerticalCenter)
            End If
        Next
    End Sub

    Private Function RowAt(pt As Point) As Integer
        If Count() = 0 Then Return -1
        Dim i As Integer = (pt.Y + _scroll) \ RowH
        If i < 0 OrElse i >= Count() Then Return -1
        Return i
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim i As Integer = RowAt(e.Location)
        If i <> _hot Then
            _hot = i
            Cursor = If(i >= 0, Cursors.Hand, Cursors.Default)
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
        Dim i As Integer = RowAt(e.Location)
        If i >= 0 Then RaiseEvent ConvoClicked(Db.GetInt(_dt.Rows(i), "id"))
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        _scroll = Math.Min(_scroll, MaxScroll())
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso _hook IsNot Nothing Then _hook.Detach()
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>Top of the chat: avatar, customer name, order number and status pill.</summary>
Public Class ChatHeader
    Inherits Control

    Private ReadOnly _fName As Font = Theme.F(15, True)
    Private ReadOnly _fSub As Font = Theme.F(11)
    Private ReadOnly _fPill As Font = Theme.F(9.5F, True)
    Private _name As String = ""
    Private _orderId As Integer
    Private _status As String = ""

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.White
    End Sub

    Public Sub Setup(name As String, orderId As Integer, status As String)
        _name = name
        _orderId = orderId
        _status = status
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(Color.White)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using pen As New Pen(Theme.Border, 1.0F)
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1)
        End Using
        If _name = "" Then Return
        InboxUi.DrawAvatar(g, New Rectangle(26, (Height - 54) \ 2, 54, 54), _name)
        TextRenderer.DrawText(g, _name, _fName, New Rectangle(96, Height \ 2 - 28, Math.Max(50, Width - 96 - 200), 28), Theme.TextDark,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.VerticalCenter)
        Dim t As String = "Order #" & _orderId.ToString()
        Dim tw As Integer = TextRenderer.MeasureText(t, _fSub, New Size(400, 30), TextFormatFlags.NoPadding).Width
        TextRenderer.DrawText(g, t, _fSub, New Rectangle(96, Height \ 2 + 2, tw + 4, 24), Theme.TextMuted, TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
        Dim fill As Color
        Dim fore As Color
        InboxUi.PillColors(_status, fill, fore)
        Dim label As String = InboxUi.StatusText(_status)
        Dim pw As Integer = TextRenderer.MeasureText(label, _fPill, New Size(300, 30), TextFormatFlags.NoPadding).Width + 24
        Dim pr As New Rectangle(96 + tw + 12, Height \ 2 + 3, pw, 24)
        Using p As GraphicsPath = Theme.RoundRectPath(pr, 12), b As New SolidBrush(fill)
            g.FillPath(b, p)
        End Using
        TextRenderer.DrawText(g, label, _fPill, pr, fore, TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
    End Sub
End Class

''' <summary>Bottom of the chat: photo button, text box and Send, or a "finished" bar when the order is over.</summary>
Public Class Composer
    Inherits Control

    Public Event TextSent(body As String)
    Public Event ImagePicked(filePath As String)

    Private ReadOnly _attach As RoundedButton
    Private ReadOnly _send As RoundedButton
    Private ReadOnly _input As New AppTextBox()
    Private ReadOnly _fLock As Font = Theme.F(11.5F)
    Private _locked As Boolean
    Private _lockText As String = ""

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = InboxUi.ChatBg
        _attach = Ui.SoftBtn("+ Photo", 100, 48)
        _send = Ui.PrimaryBtn("Send", 100, 48)
        _input.PlaceholderText = "Type a message..."
        _input.MaxLength = 400
        _input.BackColor = InboxUi.ChatBg
        Controls.Add(_attach)
        Controls.Add(_input)
        Controls.Add(_send)
        AddHandler _send.Click, Sub(a, b) DoSend()
        AddHandler _input.EnterPressed, Sub(a, b) DoSend()
        AddHandler _attach.Click, AddressOf OnAttach
    End Sub

    Public Overrides Property Text As String
        Get
            Return _input.Text
        End Get
        Set(value As String)
            _input.Text = value
        End Set
    End Property

    Public Property Locked As Boolean
        Get
            Return _locked
        End Get
        Set(value As Boolean)
            If _locked = value Then Return
            _locked = value
            _attach.Visible = Not value
            _input.Visible = Not value
            _send.Visible = Not value
            Invalidate()
        End Set
    End Property

    Public Property LockedText As String
        Get
            Return _lockText
        End Get
        Set(value As String)
            _lockText = value
            Invalidate()
        End Set
    End Property

    Private Sub DoSend()
        Dim t As String = _input.Text.Trim()
        If t = "" Then Return
        _input.Text = ""
        RaiseEvent TextSent(t)
    End Sub

    Private Sub OnAttach(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Title = "Choose a photo"
            dlg.Filter = "Photos|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files|*.*"
            If dlg.ShowDialog(FindForm()) = DialogResult.OK Then RaiseEvent ImagePicked(dlg.FileName)
        End Using
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        Dim y As Integer = (Height - 48) \ 2
        _attach.SetBounds(20, y, 100, 48)
        _send.SetBounds(Width - 20 - 100, y, 100, 48)
        _input.SetBounds(20 + 100 + 10, y, Math.Max(80, Width - 40 - 200 - 20), 48)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(InboxUi.ChatBg)
        If Not _locked Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim r As New Rectangle(20, 12, Math.Max(50, Width - 40), Math.Max(30, Height - 24))
        Using p As GraphicsPath = Theme.RoundRectPath(r, 16), b As New SolidBrush(Color.FromArgb(245, 239, 232))
            g.FillPath(b, p)
        End Using
        TextRenderer.DrawText(g, _lockText, _fLock, r, Theme.TextMuted,
                              TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
    End Sub
End Class

''' <summary>Shows a photo larger. With canSend it is a preview with Cancel / Send (DialogResult.OK = send).</summary>
Public Class ImageViewForm
    Inherits Form

    Private ReadOnly _img As Image

    Public Sub New(img As Image, canSend As Boolean)
        _img = img
        Text = If(canSend, "Send this photo?", "Photo")
        Font = Theme.F(9.75F)
        BackColor = Theme.Bg
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent

        Dim sc As Double = Math.Min(1.0, Math.Min(760.0 / img.Width, 520.0 / img.Height))
        Dim pw As Integer = Math.Max(1, CInt(img.Width * sc))
        Dim ph As Integer = Math.Max(1, CInt(img.Height * sc))
        Dim areaW As Integer = Math.Max(340, pw)
        ClientSize = New Size(areaW + 40, ph + 40 + 72)

        Dim pic As New PictureBox()
        pic.SizeMode = PictureBoxSizeMode.Zoom
        pic.Image = img
        pic.BackColor = Theme.Bg
        pic.SetBounds(20 + (areaW - pw) \ 2, 20, pw, ph)
        Controls.Add(pic)

        Dim by As Integer = ph + 20 + 16
        If canSend Then
            Dim cancel As RoundedButton = Ui.OutlineBtn("Cancel", 130, 44)
            cancel.Location = New Point(ClientSize.Width - 20 - 130 - 12 - 130, by)
            AddHandler cancel.Click, Sub(a, b)
                                         DialogResult = DialogResult.Cancel
                                         Close()
                                     End Sub
            Dim sendBtn As RoundedButton = Ui.PrimaryBtn("Send", 130, 44)
            sendBtn.Location = New Point(ClientSize.Width - 20 - 130, by)
            AddHandler sendBtn.Click, Sub(a, b)
                                          DialogResult = DialogResult.OK
                                          Close()
                                      End Sub
            Controls.Add(cancel)
            Controls.Add(sendBtn)
        Else
            Dim closeBtn As RoundedButton = Ui.PrimaryBtn("Close", 130, 44)
            closeBtn.Location = New Point(ClientSize.Width - 20 - 130, by)
            AddHandler closeBtn.Click, Sub(a, b) Close()
            Controls.Add(closeBtn)
        End If
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        MyBase.OnFormClosed(e)
        If _img IsNot Nothing Then _img.Dispose()
    End Sub
End Class
