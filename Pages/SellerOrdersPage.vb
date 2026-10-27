Imports System.Data
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>One line of an order (snapshot from order_items).</summary>
Public Class OrderLine
    Public Qty As Integer
    Public Name As String = ""
    Public SizeName As String = ""
    Public Extras As String = ""
End Class

''' <summary>One order card on the seller board. Fully custom painted; only the action button and the Decline link are real controls.</summary>
Public Class OrderCard
    Inherits Control

    Public ReadOnly OsId As Integer
    Public ReadOnly OrderId As Integer
    Public Status As String
    Public ReadOnly Username As String
    Public ReadOnly Created As DateTime
    Public ReadOnly Total As Decimal
    Public ReadOnly Lines As New List(Of OrderLine)
    Public DineText As String = ""
    Public Busy As Boolean
    Public Leaving As Boolean
    ' animation: positions are in page *content* coordinates
    Public FromX As Integer
    Public FromY As Integer
    Public TX As Integer
    Public TY As Integer

    Public Event ActionRequested As EventHandler
    Public Event DeclineRequested As EventHandler

    Private Const Pad As Integer = 22
    Private Const NoPad As TextFormatFlags = TextFormatFlags.NoPadding
    Private ReadOnly _btn As RoundedButton
    Private ReadOnly _decline As Label
    Private ReadOnly _lineY As New List(Of Integer)
    Private ReadOnly _fNum As Font
    Private ReadOnly _fBold As Font
    Private ReadOnly _fText As Font
    Private ReadOnly _fSmall As Font
    Private ReadOnly _fTiny As Font
    Private ReadOnly _fTotal As Font
    Private _div1 As Integer = 88
    Private _div2 As Integer
    Private _totalY As Integer

    Public Sub New(osId As Integer, orderId As Integer, status As String, username As String, created As DateTime, total As Decimal, items As DataTable)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or ControlStyles.SupportsTransparentBackColor, True)
        BackColor = Color.Transparent
        Me.OsId = osId
        Me.OrderId = orderId
        Me.Status = status
        Me.Username = username
        Me.Created = created
        Me.Total = total

        _fNum = Theme.F(20, True)
        _fBold = Theme.F(11, True)
        _fText = Theme.F(11)
        _fSmall = Theme.F(10)
        _fTiny = Theme.F(9)
        _fTotal = Theme.F(13, True)

        Dim dines As New List(Of String)
        For Each r As DataRow In items.Rows
            Dim l As New OrderLine()
            l.Qty = Db.GetInt(r, "quantity")
            l.Name = Db.GetStr(r, "product_name")
            l.SizeName = Db.GetStr(r, "size_name")
            l.Extras = Db.GetStr(r, "extras_text")
            Lines.Add(l)
            Dim d As String = Db.GetStr(r, "dine_option")
            If Not dines.Contains(d) Then dines.Add(d)
        Next
        If dines.Count = 1 Then
            DineText = If(dines(0) = "take_out", "Take out", "Dine in")
        ElseIf dines.Count > 1 Then
            DineText = "Dine in + Take out"
        End If

        _btn = Ui.PrimaryBtn("", 100, 46)
        _btn.Font = Theme.F(11, True)
        _btn.BackColor = Color.White
        AddHandler _btn.Click, AddressOf OnBtnClick
        _decline = Ui.Link("Decline", 10, Theme.TextMuted, False)
        _decline.BackColor = Color.White
        AddHandler _decline.Click, AddressOf OnDeclineClick
        Controls.Add(_btn)
        Controls.Add(_decline)
        ApplyStatus()
    End Sub

    Private Sub OnBtnClick(sender As Object, e As EventArgs)
        RaiseEvent ActionRequested(Me, EventArgs.Empty)
    End Sub

    Private Sub OnDeclineClick(sender As Object, e As EventArgs)
        RaiseEvent DeclineRequested(Me, EventArgs.Empty)
    End Sub

    Public Sub SetStatus(newStatus As String)
        Status = newStatus
        ApplyStatus()
    End Sub

    Private Sub ApplyStatus()
        Select Case Status
            Case "pending"
                _btn.Text = "Start preparing"
                _btn.FillColor = Theme.Primary
                _btn.HoverColor = Color.FromArgb(235, 90, 40)
            Case "preparing"
                _btn.Text = "Mark ready"
                _btn.FillColor = Color.FromArgb(32, 163, 95)
                _btn.HoverColor = Color.FromArgb(26, 140, 80)
            Case Else
                _btn.Text = "Completed"
                _btn.FillColor = Color.FromArgb(44, 30, 22)
                _btn.HoverColor = Color.FromArgb(72, 52, 40)
        End Select
        _btn.TextColor = Color.White
        _decline.Visible = (Status = "pending")
        If Width > 0 Then ApplyWidth(Width)
        Invalidate()
    End Sub

    ''' <summary>Sets the width and works out the height (it depends on the number of lines).</summary>
    Public Sub ApplyWidth(w As Integer)
        _lineY.Clear()
        Dim y As Integer = 100
        For Each l As OrderLine In Lines
            _lineY.Add(y)
            y += 28
            If l.Extras <> "" Then y += 17
        Next
        y += 6
        _div2 = y
        y += 14
        _totalY = y
        y += 40
        _btn.SetBounds(Pad, y, w - 2 * Pad, 46)
        y += 46
        If _decline.Visible Then
            y += 12
            _decline.Location = New Point(CInt((w - _decline.PreferredWidth) / 2), y)
            y += 20
        End If
        y += 20
        SetBounds(Left, Top, w, y + 4)
    End Sub

    Public Shared Function Money(d As Decimal) As String
        Dim s As String = If(d = Math.Floor(d), d.ToString("N0"), d.ToString("N2"))
        Return ChrW(&H20B1) & s
    End Function

    Public Shared Function TimeAgo(t As DateTime) As String
        Dim m As Double = (DateTime.Now - t).TotalMinutes
        If m < 1 Then Return "Just now"
        If m < 60 Then Return CInt(Math.Floor(m)).ToString() & " min"
        If m < 1440 Then Return CInt(Math.Floor(m / 60)).ToString() & " hr"
        Return t.ToString("MMM d")
    End Function

    Private Shared Function Measure(t As String, f As Font) As Integer
        Return TextRenderer.MeasureText(t, f, New Size(2000, 100), TextFormatFlags.NoPadding).Width
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim body As New Rectangle(0, 0, Width - 1, Height - 5)
        Using sh As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 3, Width - 1, Height - 5), 18), sb As New SolidBrush(Color.FromArgb(22, 90, 50, 20))
            g.FillPath(sb, sh)
        End Using
        If Status = "pending" Then
            Using p As GraphicsPath = Theme.RoundRectPath(body, 18), b As New SolidBrush(Theme.Primary)
                g.FillPath(b, p)
            End Using
            Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(body.X + 4, body.Y, body.Width - 4, body.Height), 18)
                g.FillPath(Brushes.White, p)
            End Using
        Else
            Using p As GraphicsPath = Theme.RoundRectPath(body, 18)
                g.FillPath(Brushes.White, p)
            End Using
        End If

        ' header
        TextRenderer.DrawText(g, "#" & OrderId.ToString(), _fNum, New Point(Pad, 12), Theme.TextDark, NoPad)
        TextRenderer.DrawText(g, TimeAgo(Created), _fSmall, New Rectangle(Pad, 22, Width - 2 * Pad - 2, 22), Theme.TextMuted, NoPad Or TextFormatFlags.Right)
        TextRenderer.DrawText(g, Username, _fBold, New Point(Pad, 54), Theme.TextDark, NoPad)
        If DineText <> "" Then
            Dim uw As Integer = Measure(Username, _fBold)
            Dim pw As Integer = Measure(DineText, _fTiny) + 20
            Dim pr As New Rectangle(Pad + uw + 10, 54, pw, 22)
            Using p As GraphicsPath = Theme.RoundRectPath(pr, 11), b As New SolidBrush(Color.FromArgb(241, 235, 227))
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, DineText, _fTiny, pr, Theme.TextMuted, NoPad Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
        End If

        Using pen As New Pen(Color.FromArgb(236, 228, 218), 1)
            g.DrawLine(pen, Pad, _div1, Width - Pad - 1, _div1)
            g.DrawLine(pen, Pad, _div2, Width - Pad - 1, _div2)
        End Using

        ' items
        Dim nx As Integer = Pad + 36
        Dim avail As Integer = Width - Pad - nx
        For i As Integer = 0 To Lines.Count - 1
            Dim l As OrderLine = Lines(i)
            Dim ly As Integer = _lineY(i)
            TextRenderer.DrawText(g, l.Qty.ToString() & ChrW(&HD7), _fBold, New Point(Pad, ly + 1), Theme.TextDark, NoPad)
            Dim szW As Integer = 0
            If l.SizeName <> "" Then szW = Measure(l.SizeName, _fText) + 6
            Dim nameW As Integer = Math.Min(Measure(l.Name, _fText), Math.Max(40, avail - szW))
            TextRenderer.DrawText(g, l.Name, _fText, New Rectangle(nx, ly, nameW + 4, 24), Theme.TextDark, NoPad Or TextFormatFlags.EndEllipsis)
            If l.SizeName <> "" Then
                TextRenderer.DrawText(g, l.SizeName, _fText, New Point(nx + nameW + 6, ly), Theme.TextMuted, NoPad)
            End If
            If l.Extras <> "" Then
                TextRenderer.DrawText(g, "+ " & l.Extras, _fTiny, New Rectangle(nx, ly + 25, avail, 18), Theme.TextMuted, NoPad Or TextFormatFlags.EndEllipsis)
            End If
        Next

        ' total
        TextRenderer.DrawText(g, "Total", _fSmall, New Point(Pad, _totalY + 2), Theme.TextMuted, NoPad)
        TextRenderer.DrawText(g, Money(Total), _fTotal, New Rectangle(Pad, _totalY - 2, Width - 2 * Pad - 2, 26), Theme.TextDark, NoPad Or TextFormatFlags.Right)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

''' <summary>Seller "Orders" board: New / Preparing / Ready columns with sliding cards.</summary>
Public Class SellerOrdersPage
    Inherits PageBase

    Private Const TitleY As Integer = 30
    Private Const ColTop As Integer = 100
    Private Const HeadH As Integer = 58
    Private Const CardGap As Integer = 16
    Private Const AnimMs As Integer = 340
    Private Const NoPad As TextFormatFlags = TextFormatFlags.NoPadding
    Private ReadOnly ColBg As Color = Color.FromArgb(245, 238, 230)

    Private ReadOnly _stallId As Integer
    Private ReadOnly _title As Label
    Private ReadOnly _stallLbl As Label
    Private ReadOnly _titles() As String = {"New", "Preparing", "Ready"}
    Private ReadOnly _empties() As String = {"No new orders", "Nothing in the kitchen", "Nothing ready yet"}
    Private ReadOnly _statuses() As String = {"pending", "preparing", "ready"}
    Private ReadOnly _byId As New Dictionary(Of Integer, OrderCard)
    Private ReadOnly _leaving As New List(Of OrderCard)
    Private _ordered As New List(Of OrderCard)
    Private ReadOnly _poll As New Timer()
    Private ReadOnly _anim As New Timer()
    Private ReadOnly _sw As New Stopwatch()
    Private ReadOnly _fHead As Font
    Private ReadOnly _fPill As Font
    Private ReadOnly _fEmpty As Font
    Private ReadOnly _colX(2) As Integer
    Private ReadOnly _colH(2) As Double
    Private ReadOnly _colHFrom(2) As Double
    Private ReadOnly _colHTo(2) As Double
    Private _colW As Integer
    Private _cw As Integer
    Private _sig As String = ""
    Private _loaded As Boolean

    Public Sub New(stallId As Integer, stallName As String)
        _stallId = stallId
        _title = Ui.Lbl("Orders", 26, True)
        _stallLbl = Ui.Lbl(stallName, 13, True, Theme.TextMuted)
        Controls.Add(_title)
        Controls.Add(_stallLbl)
        _fHead = Theme.F(13, True)
        _fPill = Theme.F(9.5F, True)
        _fEmpty = Theme.F(10.5F)

        _anim.Interval = 15
        AddHandler _anim.Tick, AddressOf OnAnimTick
        _poll.Interval = 4000
        AddHandler _poll.Tick, AddressOf OnPoll

        SyncBoard(True)
        _poll.Start()
    End Sub

    ' ---------------- helpers ----------------
    ''' <summary>How far the page is scrolled. The title label is the anchor: it sits at TitleY when not scrolled.</summary>
    Private Function ScrollOffset() As Integer
        Return TitleY - _title.Top
    End Function

    Private Function CountOf(status As String) As Integer
        Dim n As Integer = 0
        For Each c As OrderCard In _ordered
            If c.Status = status Then n += 1
        Next
        Return n
    End Function

    Private Shared Function Measure(t As String, f As Font) As Integer
        Return TextRenderer.MeasureText(t, f, New Size(2000, 100), TextFormatFlags.NoPadding).Width
    End Function

    ' ---------------- layout ----------------
    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        FinishAnim()
        _cw = contentWidth
        _title.Location = New Point(M, TitleY)
        _stallLbl.Location = New Point(M + contentWidth - _stallLbl.PreferredWidth, TitleY + 14)
        Dim gap As Integer = 20
        _colW = Math.Max(200, CInt(Math.Floor((contentWidth - 2 * gap) / 3)))
        For i As Integer = 0 To 2
            _colX(i) = M + i * (_colW + gap)
        Next
        ComputeLayout()
        SnapAll()
        UpdateContentHeight()
    End Sub

    ''' <summary>Works out the target position of every card and the target height of every column.</summary>
    Private Sub ComputeLayout()
        If _cw = 0 Then Return
        Dim cardW As Integer = _colW - 32
        For i As Integer = 0 To 2
            Dim y As Integer = ColTop + HeadH
            Dim n As Integer = 0
            For Each c As OrderCard In _ordered
                If c.Status <> _statuses(i) Then Continue For
                c.ApplyWidth(cardW)
                c.TX = _colX(i) + 16
                c.TY = y
                y += c.Height + CardGap
                n += 1
            Next
            If n = 0 Then
                _colHTo(i) = HeadH + 80
            Else
                _colHTo(i) = (y - CardGap) - ColTop + 18
            End If
        Next
    End Sub

    Private Sub SnapAll()
        Dim off As Integer = ScrollOffset()
        For Each c As OrderCard In _ordered
            c.SetBounds(c.TX, c.TY - off, c.Width, c.Height)
        Next
        For i As Integer = 0 To 2
            _colH(i) = _colHTo(i)
        Next
        Invalidate()
    End Sub

    Private Sub UpdateContentHeight()
        Dim m As Double = 0
        For i As Integer = 0 To 2
            m = Math.Max(m, _colHTo(i))
        Next
        SetContentHeight(ColTop + CInt(m) + 30)
    End Sub

    ' ---------------- painting (columns are painted by the page, cards sit on top) ----------------
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If _cw = 0 Then Return
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim off As Integer = ScrollOffset()
        Using accent As New SolidBrush(Theme.Primary)
            g.FillRectangle(accent, M, 10 - off, 46, 5)
        End Using
        For i As Integer = 0 To 2
            Dim r As New Rectangle(_colX(i), ColTop - off, _colW, CInt(_colH(i)))
            Using p As GraphicsPath = Theme.RoundRectPath(r, 22), b As New SolidBrush(ColBg)
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, _titles(i), _fHead, New Point(r.X + 22, r.Y + 18), Theme.TextDark, NoPad)
            Dim n As Integer = CountOf(_statuses(i))
            Dim txt As String = n.ToString()
            Dim pw As Integer = Math.Max(26, Measure(txt, _fPill) + 16)
            Dim pr As New Rectangle(r.X + 22 + Measure(_titles(i), _fHead) + 10, r.Y + 19, pw, 24)
            Dim hot As Boolean = (i = 0 AndAlso n > 0)
            Using p As GraphicsPath = Theme.RoundRectPath(pr, 12), b As New SolidBrush(If(hot, Theme.Primary, Color.White))
                g.FillPath(b, p)
            End Using
            TextRenderer.DrawText(g, txt, _fPill, pr, If(hot, Color.White, Theme.TextDark), NoPad Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            If n = 0 Then
                TextRenderer.DrawText(g, _empties(i), _fEmpty, New Point(r.X + 22, r.Y + HeadH + 8), Theme.TextMuted, NoPad)
            End If
        Next
    End Sub

    ' ---------------- data sync ----------------
    Private Sub OnPoll(sender As Object, e As EventArgs)
        SyncBoard(False)
    End Sub

    ''' <summary>Reads the board from MySQL and updates the cards. Changed cards slide to their new place.</summary>
    Private Sub SyncBoard(force As Boolean)
        Dim dt As DataTable
        Try
            dt = SellerSvc.GetBoard(_stallId)
        Catch
            Return
        End Try
        Dim sb As New System.Text.StringBuilder()
        For Each r As DataRow In dt.Rows
            sb.Append(Db.GetInt(r, "id")).Append(":").Append(Db.GetStr(r, "status")).Append(";")
        Next
        Dim sig As String = sb.ToString()
        If sig = _sig AndAlso Not force Then
            For Each c As OrderCard In _ordered
                c.Invalidate()
            Next
            Return
        End If
        _sig = sig
        FinishAnim()

        Dim ordered As New List(Of OrderCard)
        Dim fresh As New List(Of OrderCard)
        Dim seen As New HashSet(Of Integer)
        For Each r As DataRow In dt.Rows
            Dim id As Integer = Db.GetInt(r, "id")
            Dim status As String = Db.GetStr(r, "status")
            seen.Add(id)
            Dim c As OrderCard = Nothing
            If _byId.TryGetValue(id, c) Then
                If c.Status <> status Then c.SetStatus(status)
            Else
                c = New OrderCard(id, Db.GetInt(r, "order_id"), status, Db.GetStr(r, "username"), Db.GetDate(r, "created_at"),
                                  Db.GetDec(r, "subtotal"), OrderSvc.GetOrderItems(id))
                AddHandler c.ActionRequested, AddressOf OnAction
                AddHandler c.DeclineRequested, AddressOf OnDecline
                _byId(id) = c
                Controls.Add(c)
                fresh.Add(c)
            End If
            ordered.Add(c)
        Next
        For Each id As Integer In New List(Of Integer)(_byId.Keys)
            If seen.Contains(id) Then Continue For
            Dim gone As OrderCard = _byId(id)
            gone.Leaving = True
            _byId.Remove(id)
            _leaving.Add(gone)
        Next
        _ordered = ordered

        If _cw = 0 Then
            ' not laid out yet: the first LayoutPage will place everything
            For Each c As OrderCard In _leaving
                Controls.Remove(c)
                c.Dispose()
            Next
            _leaving.Clear()
            _loaded = True
            Return
        End If

        ComputeLayout()
        Dim off As Integer = ScrollOffset()
        For Each c As OrderCard In fresh
            c.SetBounds(c.TX, c.TY - off, c.Width, c.Height)
        Next
        UpdateContentHeight()
        StartAnim()

        If _loaded AndAlso fresh.Count > 0 Then
            Try
                System.Media.SystemSounds.Asterisk.Play()
            Catch
            End Try
            Toast.Show(Me, If(fresh.Count = 1, "New order #" & fresh(0).OrderId.ToString(), fresh.Count.ToString() & " new orders"))
        End If
        _loaded = True
    End Sub

    ' ---------------- animation ----------------
    Private Sub StartAnim()
        Dim off As Integer = ScrollOffset()
        For Each c As OrderCard In _ordered
            c.FromX = c.Left
            c.FromY = c.Top + off
        Next
        For Each c As OrderCard In _leaving
            c.FromX = c.Left
            c.FromY = c.Top + off
            c.TX = If(c.Status = "pending", -(c.Width + 60), ClientSize.Width + 60)
            c.TY = c.FromY
            c.BringToFront()
        Next
        For i As Integer = 0 To 2
            _colHFrom(i) = _colH(i)
        Next
        _sw.Restart()
        _anim.Start()
    End Sub

    Private Sub Place(c As OrderCard, k As Double, off As Integer)
        Dim x As Integer = CInt(c.FromX + (c.TX - c.FromX) * k)
        Dim y As Integer = CInt(c.FromY + (c.TY - c.FromY) * k) - off
        c.SetBounds(x, y, c.Width, c.Height)
    End Sub

    Private Sub OnAnimTick(sender As Object, e As EventArgs)
        Dim t As Double = Math.Min(1.0, _sw.ElapsedMilliseconds / CDbl(AnimMs))
        Dim k As Double = 1.0 - Math.Pow(1.0 - t, 3)
        Dim off As Integer = ScrollOffset()
        For Each c As OrderCard In _ordered
            Place(c, k, off)
        Next
        For Each c As OrderCard In _leaving
            Place(c, k, off)
        Next
        For i As Integer = 0 To 2
            _colH(i) = _colHFrom(i) + (_colHTo(i) - _colHFrom(i)) * k
        Next
        Invalidate()
        If t >= 1.0 Then FinishAnim()
    End Sub

    ''' <summary>Jump to the end of a running animation (also used before any new sync / relayout).</summary>
    Private Sub FinishAnim()
        If Not _anim.Enabled Then Return
        _anim.Stop()
        For Each c As OrderCard In _leaving
            Controls.Remove(c)
            c.Dispose()
        Next
        _leaving.Clear()
        SnapAll()
        UpdateContentHeight()
    End Sub

    ' ---------------- seller actions ----------------
    Private Sub OnAction(sender As Object, e As EventArgs)
        Dim c As OrderCard = DirectCast(sender, OrderCard)
        If c.Busy OrElse c.Leaving Then Return
        Dim nextStatus As String = ""
        Select Case c.Status
            Case "pending" : nextStatus = "preparing"
            Case "preparing" : nextStatus = "ready"
            Case "ready" : nextStatus = "completed"
        End Select
        If nextStatus = "" Then Return
        c.Busy = True
        Dim ok As Boolean = False
        Try
            ok = SellerSvc.SetStatus(c.OsId, _stallId, c.Status, nextStatus, "")
        Catch ex As Exception
            c.Busy = False
            Toast.Show(Me, "Could not update the order: " & ex.Message, True)
            Return
        End Try
        c.Busy = False
        If Not ok Then Toast.Show(Me, "That order was already updated.", True)
        SyncBoard(True)
    End Sub

    Private Sub OnDecline(sender As Object, e As EventArgs)
        Dim c As OrderCard = DirectCast(sender, OrderCard)
        If c.Busy OrElse c.Leaving OrElse c.Status <> "pending" Then Return
        Dim reason As String = ""
        Using f As New DeclineReasonForm(c.OrderId)
            If f.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            reason = f.Reason
        End Using
        c.Busy = True
        Dim ok As Boolean = False
        Try
            ok = SellerSvc.SetStatus(c.OsId, _stallId, "pending", "cancelled", reason)
        Catch ex As Exception
            c.Busy = False
            Toast.Show(Me, "Could not decline the order: " & ex.Message, True)
            Return
        End Try
        c.Busy = False
        If Not ok Then Toast.Show(Me, "That order was already updated.", True)
        SyncBoard(True)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _poll.Stop()
            _poll.Dispose()
            _anim.Stop()
            _anim.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
