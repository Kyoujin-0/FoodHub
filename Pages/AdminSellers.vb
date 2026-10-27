Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Windows.Forms

''' <summary>Painted seller table (header + rows). The page scrolls, so the control is as tall as all its rows.</summary>
Public Class AdminSellerList
    Inherits Control

    Public Const HeaderH As Integer = 56
    Public Const RowH As Integer = 88

    Public Event RowClicked(userId As Integer)
    Public EmptyText As String = "No sellers found."

    Private ReadOnly _items As New List(Of AdminSellerRow)
    Private _hot As Integer = -1
    Private ReadOnly _fHead As Font = Theme.F(9.5F, True)
    Private ReadOnly _fTitle As Font = Theme.F(13, True)
    Private ReadOnly _fStall As Font = Theme.F(12, True)
    Private ReadOnly _fSub As Font = Theme.F(10)
    Private ReadOnly _fContact As Font = Theme.F(12)
    Private ReadOnly _fPill As Font = Theme.F(10, True)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.White
    End Sub

    Public Sub SetItems(items As List(Of AdminSellerRow))
        _items.Clear()
        _items.AddRange(items)
        _hot = -1
        Invalidate()
    End Sub

    Public Function DesiredHeight() As Integer
        Return HeaderH + Math.Max(RowH * 2, _items.Count * RowH)
    End Function

    Public Shared Function PillText(status As String) As String
        Select Case status
            Case "active" : Return "Active"
            Case "pending" : Return "Pending"
            Case "rejected" : Return "Rejected"
            Case Else : Return "Deactivated"
        End Select
    End Function

    Public Shared Function PillFore(status As String) As Color
        Select Case status
            Case "active" : Return Theme.Success
            Case "pending" : Return Color.FromArgb(161, 98, 7)
            Case "rejected" : Return Theme.Danger
            Case Else : Return Theme.TextMuted
        End Select
    End Function

    Public Shared Function PillBack(status As String) As Color
        Select Case status
            Case "active" : Return Theme.SuccessSoft
            Case "pending" : Return Color.FromArgb(254, 243, 199)
            Case "rejected" : Return Theme.DangerSoft
            Case Else : Return Color.FromArgb(240, 235, 229)
        End Select
    End Function

    Private Function ColStall() As Integer
        Return CInt(Width * 0.4)
    End Function

    Private Function ColContact() As Integer
        Return CInt(Width * 0.62)
    End Function

    Private Function ColStatus() As Integer
        Return CInt(Width * 0.82)
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(Color.White)
        g.SmoothingMode = SmoothingMode.AntiAlias

        ' header
        TextRenderer.DrawText(g, "SELLER", _fHead, New Rectangle(4, 0, 200, HeaderH), Theme.TextMuted, TxtLeft)
        TextRenderer.DrawText(g, "STALL", _fHead, New Rectangle(ColStall(), 0, 200, HeaderH), Theme.TextMuted, TxtLeft)
        TextRenderer.DrawText(g, "CONTACT", _fHead, New Rectangle(ColContact(), 0, 200, HeaderH), Theme.TextMuted, TxtLeft)
        TextRenderer.DrawText(g, "STATUS", _fHead, New Rectangle(ColStatus(), 0, 200, HeaderH), Theme.TextMuted, TxtLeft)
        Using pen As New Pen(Color.FromArgb(236, 226, 216), 1.0F)
            g.DrawLine(pen, 0, HeaderH - 1, Width, HeaderH - 1)
        End Using

        If _items.Count = 0 Then
            TextRenderer.DrawText(g, EmptyText, _fStall, New Rectangle(0, HeaderH, Width, RowH * 2), Theme.TextMuted, TxtCenter)
            Return
        End If

        For i As Integer = 0 To _items.Count - 1
            Dim y0 As Integer = HeaderH + i * RowH
            If y0 + RowH < e.ClipRectangle.Top OrElse y0 > e.ClipRectangle.Bottom Then Continue For
            Dim s As AdminSellerRow = _items(i)
            Dim dimmed As Boolean = (s.Status = "disabled" OrElse s.Status = "rejected")
            Dim main As Color = If(dimmed, Theme.TextMuted, Theme.TextDark)

            If i = _hot Then
                Using b As New SolidBrush(Color.FromArgb(253, 249, 245))
                    g.FillRectangle(b, 0, y0, Width, RowH)
                End Using
            End If

            ' seller
            Dim ar As New Rectangle(4, y0 + (RowH - 54) \ 2, 54, 54)
            Avatars.Draw(g, ar, s.Username, s.AvatarPath)
            If dimmed Then
                Using b As New SolidBrush(Color.FromArgb(150, 255, 255, 255))
                    g.FillEllipse(b, ar)
                End Using
            End If
            Dim sellerW As Integer = Math.Max(60, ColStall() - 72 - 16)
            TextRenderer.DrawText(g, s.Username, _fTitle, New Rectangle(72, y0 + 16, sellerW, 26), main, TxtLeft)
            Dim joined As String = If(s.Joined = Date.MinValue, "", "Joined " & s.Joined.ToString("MMM d, yyyy", CultureInfo.InvariantCulture))
            TextRenderer.DrawText(g, joined, _fSub, New Rectangle(72, y0 + 44, sellerW, 22), Theme.TextMuted, TxtLeft)

            ' stall
            Dim stallW As Integer = Math.Max(60, ColContact() - ColStall() - 16)
            TextRenderer.DrawText(g, If(s.StallName = "", "No stall", s.StallName), _fStall, New Rectangle(ColStall(), y0 + 16, stallW, 26), Theme.TextDark, TxtLeft)
            TextRenderer.DrawText(g, If(s.Category = "", "No category", s.Category), _fSub, New Rectangle(ColStall(), y0 + 44, stallW, 22), Theme.TextMuted, TxtLeft)

            ' contact
            Dim contactW As Integer = Math.Max(60, ColStatus() - ColContact() - 16)
            If s.Contact = "" Then
                TextRenderer.DrawText(g, "None", _fContact, New Rectangle(ColContact(), y0, contactW, RowH), Theme.TextMuted, TxtLeft)
            Else
                TextRenderer.DrawText(g, s.Contact, _fContact, New Rectangle(ColContact(), y0, contactW, RowH), Theme.TextDark, TxtLeft)
            End If

            ' status pill + chevron
            Dim pt As String = PillText(s.Status)
            Dim pw As Integer = TextW(pt, _fPill) + 28
            DrawPill(g, New Rectangle(ColStatus(), y0 + (RowH - 28) \ 2, pw, 28), pt, PillFore(s.Status), PillBack(s.Status), _fPill)
            Icons.Draw(g, "chev_r", New Rectangle(Width - 30, y0 + (RowH - 18) \ 2, 18, 18), If(i = _hot, Theme.Primary, Theme.TextMuted))

            Using pen As New Pen(Color.FromArgb(240, 232, 224), 1.0F)
                g.DrawLine(pen, 0, y0 + RowH - 1, Width, y0 + RowH - 1)
            End Using
        Next
    End Sub

    Private Function RowAt(y As Integer) As Integer
        If y < HeaderH Then Return -1
        Dim i As Integer = (y - HeaderH) \ RowH
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
        If i >= 0 Then RaiseEvent RowClicked(_items(i).UserId)
    End Sub
End Class

''' <summary>Details of one seller + the actions that fit their status. ChosenAction = approve | reject | deactivate | reactivate.</summary>
Public Class AdminSellerDialog
    Inherits Form

    Public ChosenAction As String = ""

    Private ReadOnly _s As AdminSellerRow
    Private ReadOnly _fName As Font = Theme.F(16, True)
    Private ReadOnly _fCap As Font = Theme.F(10)
    Private ReadOnly _fVal As Font = Theme.F(11, True)
    Private ReadOnly _fPill As Font = Theme.F(10, True)

    Public Sub New(s As AdminSellerRow)
        _s = s
        Text = "Seller"
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        BackColor = Color.White
        Font = Theme.F(10)
        DoubleBuffered = True

        Dim innerW As Integer = 404
        Dim y As Integer = 124 + 4 * 36 + 20
        Select Case s.Status
            Case "pending"
                Dim half As Integer = (innerW - 12) \ 2
                AddButton(Ui.PrimaryBtn("Approve", half, 48), 28, y, "approve")
                AddButton(Ui.OutlineBtn("Reject", half, 48), 28 + half + 12, y, "reject")
            Case "active"
                AddButton(Ui.OutlineBtn("Deactivate account", innerW, 48), 28, y, "deactivate")
            Case "rejected"
                AddButton(Ui.PrimaryBtn("Approve", innerW, 48), 28, y, "approve")
            Case Else
                AddButton(Ui.PrimaryBtn("Reactivate account", innerW, 48), 28, y, "reactivate")
        End Select
        ClientSize = New Size(460, y + 48 + 28)
    End Sub

    Private Sub AddButton(b As RoundedButton, x As Integer, y As Integer, action As String)
        b.Location = New Point(x, y)
        AddHandler b.Click, Sub(sender, e)
                                ChosenAction = action
                                DialogResult = DialogResult.OK
                            End Sub
        Controls.Add(b)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Avatars.Draw(g, New Rectangle(28, 26, 64, 64), _s.Username, _s.AvatarPath)
        TextRenderer.DrawText(g, _s.Username, _fName, New Rectangle(108, 28, 330, 28), Theme.TextDark, TxtLeft)
        Dim pt As String = AdminSellerList.PillText(_s.Status)
        Dim pw As Integer = TextW(pt, _fPill) + 28
        DrawPill(g, New Rectangle(108, 60, pw, 28), pt, AdminSellerList.PillFore(_s.Status), AdminSellerList.PillBack(_s.Status), _fPill)
        Using pen As New Pen(Color.FromArgb(236, 226, 216), 1.0F)
            g.DrawLine(pen, 28, 108, ClientSize.Width - 28, 108)
        End Using

        Dim caps() As String = {"Stall", "Category", "Contact", "Joined"}
        Dim vals() As String = {
            If(_s.StallName = "", "No stall", _s.StallName),
            If(_s.Category = "", "No category", _s.Category),
            If(_s.Contact = "", "None", _s.Contact),
            If(_s.Joined = Date.MinValue, "", _s.Joined.ToString("MMM d, yyyy", CultureInfo.InvariantCulture))}
        For i As Integer = 0 To 3
            Dim y As Integer = 124 + i * 36
            TextRenderer.DrawText(g, caps(i), _fCap, New Rectangle(28, y, 100, 30), Theme.TextMuted, TxtLeft)
            TextRenderer.DrawText(g, vals(i), _fVal, New Rectangle(140, y, 292, 30), Theme.TextDark, TxtLeft)
        Next
    End Sub
End Class

''' <summary>Admin Sellers tab: search, All / Active / Deactivated / Requests tabs, seller table, approve / reject / deactivate.</summary>
Public Class AdminSellersPage
    Inherits PageBase

    Private ReadOnly _accent As New Panel()
    Private ReadOnly _title As Label
    Private ReadOnly _note As Label
    Private ReadOnly _search As New AppTextBox()
    Private ReadOnly _tabs As New SegmentTabs()
    Private ReadOnly _card As New CardPanel()
    Private ReadOnly _list As New AdminSellerList()
    Private _all As New List(Of AdminSellerRow)

    Public Sub New()
        _accent.BackColor = Theme.Primary
        _accent.Size = New Size(48, 5)
        _title = Ui.Lbl("Sellers", 26, True)
        _note = Ui.Lbl("", 11, False, Theme.Danger)

        _search.LeftIcon = "search"
        _search.PlaceholderText = "Search username, stall or contact"
        _search.Font = Theme.F(11)
        _search.MaxLength = 40
        _search.BackColor = Theme.Bg

        _tabs.SetItems(New String() {"All", "Active", "Deactivated", "Requests"}, New Integer() {0, 0, 0, 0})
        _tabs.Size = New Size(500, 46)

        _card.Controls.Add(_list)
        Controls.Add(_accent)
        Controls.Add(_title)
        Controls.Add(_note)
        Controls.Add(_search)
        Controls.Add(_tabs)
        Controls.Add(_card)

        AddHandler _search.Inner.TextChanged, Sub(s, e) ApplyFilter()
        AddHandler _tabs.SelectedChanged, Sub(s, e) ApplyFilter()
        AddHandler _list.RowClicked, AddressOf OpenSeller

        SafeLoad()
    End Sub

    Private Sub SafeLoad()
        Try
            _all = AdminSellerSvc.GetSellers()
            _note.Text = ""
            Dim active As Integer = 0
            Dim deact As Integer = 0
            Dim pending As Integer = 0
            For Each s As AdminSellerRow In _all
                Select Case s.Status
                    Case "active" : active += 1
                    Case "pending" : pending += 1
                    Case Else : deact += 1
                End Select
            Next
            _tabs.SetBadge(0, _all.Count)
            _tabs.SetBadge(1, active)
            _tabs.SetBadge(2, deact)
            _tabs.SetBadge(3, pending)
        Catch ex As Exception
            _all = New List(Of AdminSellerRow)
            _note.Text = "Could not load the sellers: " & ex.Message
        End Try
        ApplyFilter()
    End Sub

    Private Sub ApplyFilter()
        Dim q As String = _search.Text.Trim().ToLowerInvariant()
        Dim items As New List(Of AdminSellerRow)
        For Each s As AdminSellerRow In _all
            Select Case _tabs.SelectedIndex
                Case 1 : If s.Status <> "active" Then Continue For
                Case 2 : If s.Status <> "disabled" AndAlso s.Status <> "rejected" Then Continue For
                Case 3 : If s.Status <> "pending" Then Continue For
            End Select
            If q <> "" Then
                Dim hay As String = (s.Username & " " & s.StallName & " " & s.Contact & " " & s.Category).ToLowerInvariant()
                If Not hay.Contains(q) Then Continue For
            End If
            items.Add(s)
        Next
        Select Case _tabs.SelectedIndex
            Case 3 : _list.EmptyText = "No pending seller requests."
            Case Else : _list.EmptyText = "No sellers found."
        End Select
        _list.SetItems(items)
        RelayoutKeepScroll()
    End Sub

    Private Sub OpenSeller(userId As Integer)
        Dim s As AdminSellerRow = Nothing
        For Each r As AdminSellerRow In _all
            If r.UserId = userId Then s = r
        Next
        If s Is Nothing Then Return

        Dim action As String
        Using dlg As New AdminSellerDialog(s)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            action = dlg.ChosenAction
        End Using

        Dim newStatus As String = ""
        Dim question As String = ""
        Dim done As String = ""
        Dim stall As String = If(s.StallName = "", "their stall", s.StallName)
        Select Case action
            Case "approve"
                newStatus = "active"
                question = "Approve " & s.Username & "? They can log in and " & stall & " opens right away."
                done = s.Username & " approved"
            Case "reject"
                newStatus = "rejected"
                question = "Reject the request of " & s.Username & "? They will not be able to log in."
                done = "Request rejected"
            Case "deactivate"
                newStatus = "disabled"
                question = "Deactivate " & s.Username & "? They will not be able to log in and " & stall & " disappears from the customer app."
                done = s.Username & " deactivated"
            Case "reactivate"
                newStatus = "active"
                question = "Reactivate " & s.Username & "? They can log in again and " & stall & " opens."
                done = s.Username & " reactivated"
            Case Else
                Return
        End Select
        If Not Ui.Confirm(question) Then Return

        Dim err As String = ""
        If Not AdminSellerSvc.SetStatus(s.UserId, newStatus, err) Then
            Toast.Show(FindForm(), err, True)
            Return
        End If
        Toast.Show(FindForm(), done)
        SafeLoad()
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim right As Integer = M + contentWidth
        _accent.Location = New Point(M, 22)
        _title.Location = New Point(M, 34)
        _note.Location = New Point(M, 90)

        _tabs.SetBounds(right - 500, 38, 500, 46)
        _search.SetBounds(_tabs.Left - 16 - 340, 36, 340, 50)

        Dim listH As Integer = _list.DesiredHeight()
        Dim cardH As Integer = listH + 28
        _card.SetBounds(M, 116, contentWidth, cardH)
        _list.SetBounds(28, 8, Math.Max(100, contentWidth - 56), listH)
        SetContentHeight(116 + cardH + 40)
    End Sub
End Class
