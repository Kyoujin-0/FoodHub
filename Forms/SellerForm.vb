Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Seller window: top bar (stall, tabs, Open switch, profile) and the current page below it.</summary>
Public Class SellerForm
    Inherits Form

    Public LoggedOut As Boolean

    Private ReadOnly _bar As New SellerTopBar()
    Private _page As Control
    Private _stallId As Integer
    Private _stallName As String = ""
    Private _isOpen As Boolean

    Public Sub New()
        Text = Theme.AppName & " - Seller"
        BackColor = Theme.Bg
        Font = Theme.F(10)
        MinimumSize = New Size(1200, 720)
        Size = New Size(1366, 820)
        StartPosition = FormStartPosition.CenterScreen
        DoubleBuffered = True

        Dim st As DataRow = SellerSvc.GetMyStall()
        Dim logo As Image = Nothing
        If st IsNot Nothing Then
            _stallId = Db.GetInt(st, "id")
            _stallName = Db.GetStr(st, "name")
            _isOpen = Db.GetBool(st, "is_open")
            logo = Ui.LoadImage(Db.GetStr(st, "logo_path"))
        End If
        _bar.Setup(_stallName, logo, _isOpen)
        Controls.Add(_bar)
        AddHandler _bar.TabClicked, AddressOf OnTab
        AddHandler _bar.OpenClicked, AddressOf OnOpenToggle
        AddHandler _bar.ProfileClicked, AddressOf ShowAccount
        ShowTab(0)
    End Sub

    Private Sub DoLayoutAll()
        _bar.SetBounds(32, 14, Math.Max(200, ClientSize.Width - 64), 78)
        If _page IsNot Nothing Then
            _page.SetBounds(0, 96, ClientSize.Width, Math.Max(100, ClientSize.Height - 96))
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        DoLayoutAll()
    End Sub

    Private Sub ClearPage()
        If _page IsNot Nothing Then
            Controls.Remove(_page)
            _page.Dispose()
            _page = Nothing
        End If
    End Sub

    Private Sub ShowTab(index As Integer)
        ClearPage()
        Select Case index
            Case 0 : _page = New SellerOrdersPage(_stallId, _stallName)
            Case 1 : _page = New SellerMenuView(_stallId, _stallName)
            Case 2 : _page = New SellerReviewsPage(_stallId, _stallName)
            Case 3 : _page = New InventoryPage(_stallId)
            Case 4 : _page = New SellerInboxPage(_stallId, _stallName)
            Case Else
                Dim sp As New SellerStorePage(_stallId)
                AddHandler sp.StoreSaved, AddressOf OnStoreSaved
                _page = sp
        End Select
        Controls.Add(_page)
        _bar.SetActive(index)
        DoLayoutAll()
    End Sub

    ''' <summary>Account settings (photo, username, password, log out), opened from the profile in the top bar.</summary>
    Private Sub ShowAccount()
        ClearPage()
        Dim p As New AccountPage()
        AddHandler p.Saved, Sub() _bar.Invalidate()
        AddHandler p.LogoutRequested, AddressOf OnLogout
        _page = p
        Controls.Add(_page)
        _bar.SetActive(-1)
        DoLayoutAll()
    End Sub

    Private Sub OnTab(index As Integer)
        ShowTab(index)
    End Sub

    Private Sub OnStoreSaved(name As String, logoPath As String)
        _stallName = name
        _bar.Setup(name, InboxSvc.LoadImg(logoPath), _isOpen)
    End Sub

    Private Sub OnOpenToggle()
        Dim nowOpen As Boolean = Not _isOpen
        If Not nowOpen Then
            If Not Ui.Confirm("Close your stall? Customers will not be able to place new orders until you open it again.") Then Return
        End If
        Try
            SellerSvc.SetOpen(_stallId, nowOpen)
        Catch ex As Exception
            Toast.Show(Me, "Could not change the stall status: " & ex.Message, True)
            Return
        End Try
        _isOpen = nowOpen
        _bar.SetOpen(nowOpen)
        Toast.Show(Me, If(nowOpen, "Your stall is now open", "Your stall is now closed"))
    End Sub

    Private Sub OnLogout()
        If Not Ui.Confirm("Log out of your seller account?") Then Return
        LoggedOut = True
        Close()
    End Sub
End Class

''' <summary>Placeholder for seller sections that are not built yet.</summary>
Public Class SellerPlaceholderPage
    Inherits PageBase

    Private ReadOnly _t As Label
    Private ReadOnly _s As Label

    Public Sub New(title As String)
        _t = Ui.Lbl(title, 26, True)
        _s = Ui.Lbl("This section is coming soon.", 12, False, Theme.TextMuted)
        Controls.Add(_t)
        Controls.Add(_s)
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        _t.Location = New Point(M, 30)
        _s.Location = New Point(M, 84)
        SetContentHeight(140)
    End Sub
End Class

''' <summary>Rounded white bar: stall logo + name, Orders/Menu/Reviews/Inventory/Inbox/Store tabs, Open switch, profile (opens Account settings).</summary>
Public Class SellerTopBar
    Inherits Control

    Public Event TabClicked(index As Integer)
    Public Event OpenClicked()
    Public Event ProfileClicked()

    Private ReadOnly _names() As String = {"Orders", "Menu", "Reviews", "Inventory", "Inbox", "Store"}
    Private ReadOnly _icons() As String = {"receipt", "menu", "star", "cart", "message", "home"}
    Private ReadOnly _rects(5) As Rectangle
    Private ReadOnly _fName As Font = Theme.F(15, True)
    Private ReadOnly _fTab As Font = Theme.F(12)
    Private ReadOnly _fTabBold As Font = Theme.F(12, True)
    Private ReadOnly _fOpen As Font = Theme.F(11, True)
    Private ReadOnly _fUser As Font = Theme.F(10.5F, True)
    Private ReadOnly _fRole As Font = Theme.F(9)
    Private _openRect As Rectangle
    Private _profileRect As Rectangle
    Private _compactProfile As Boolean
    Private _name As String = ""
    Private _logo As Image
    Private _isOpen As Boolean
    Private _active As Integer
    Private _hot As Integer = -1 ' 0-5 tabs, 10 open, 11 profile

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg
    End Sub

    Public Sub Setup(stallName As String, logo As Image, isOpen As Boolean)
        _name = stallName
        _logo = logo
        _isOpen = isOpen
        Invalidate()
    End Sub

    Public Sub SetActive(index As Integer)
        _active = index
        Invalidate()
    End Sub

    Public Sub SetOpen(isOpen As Boolean)
        _isOpen = isOpen
        Invalidate()
    End Sub

    Private Shared Function Measure(t As String, f As Font) As Integer
        Return TextRenderer.MeasureText(t, f, New Size(2000, 100), TextFormatFlags.NoPadding).Width
    End Function

    Private Sub LayoutItems()
        Dim barH As Integer = Height - 6
        Dim x As Integer = 24 + 54 + 14
        Dim nameW As Integer = Math.Min(130, Measure(_name, _fName))
        x += nameW + 24
        For i As Integer = 0 To 5
            Dim f As Font = If(i = _active, _fTabBold, _fTab)
            Dim w As Integer = 12 + 22 + 6 + Measure(_names(i), f) + 12
            _rects(i) = New Rectangle(x, (barH - 54) \ 2, w, 54)
            x += w + 6
        Next
        _openRect = New Rectangle(Width - 24 - 104, (barH - 46) \ 2, 104, 46)
        ' profile chip: avatar + username + role; only the avatar when the bar is too narrow
        Dim userW As Integer = Math.Min(110, Measure(Session.Username, _fUser))
        Dim fullW As Integer = 44 + 10 + userW + 8
        Dim avail As Integer = _openRect.X - 16 - (_rects(5).Right + 12)
        _compactProfile = (avail < fullW)
        Dim pw As Integer = If(_compactProfile, 44, fullW)
        _profileRect = New Rectangle(_openRect.X - 16 - pw, (barH - 48) \ 2, pw, 48)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        LayoutItems()
        Dim barH As Integer = Height - 6
        Using sh As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 3, Width - 1, barH), 28), sb As New SolidBrush(Color.FromArgb(18, 90, 50, 20))
            g.FillPath(sb, sh)
        End Using
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, barH), 28)
            g.FillPath(Brushes.White, p)
        End Using

        ' logo + name
        DrawLogo(g, New Rectangle(24, (barH - 54) \ 2, 54, 54))
        Dim nameW As Integer = Math.Min(130, Measure(_name, _fName))
        TextRenderer.DrawText(g, _name, _fName, New Rectangle(24 + 54 + 14, (barH - 30) \ 2, nameW + 4, 30), Theme.TextDark,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.VerticalCenter)

        ' tabs
        For i As Integer = 0 To 5
            Dim r As Rectangle = _rects(i)
            Dim isActive As Boolean = (i = _active)
            If isActive OrElse i = _hot Then
                Using p As GraphicsPath = Theme.RoundRectPath(r, 27), b As New SolidBrush(If(isActive, Theme.PrimarySoft, Color.FromArgb(250, 243, 236)))
                    g.FillPath(b, p)
                End Using
            End If
            Dim col As Color = If(isActive, Theme.Primary, Theme.TextDark)
            Dim ir As New Rectangle(r.X + 12, r.Y + (r.Height - 22) \ 2, 22, 22)
            If _icons(i) = "menu" Then
                DrawMenuIcon(g, ir, col)
            Else
                Icons.Draw(g, _icons(i), ir, col)
            End If
            TextRenderer.DrawText(g, _names(i), If(isActive, _fTabBold, _fTab), New Rectangle(ir.Right + 6, r.Y, r.Width, r.Height), col,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
        Next

        ' profile + open switch
        Dim pr As Rectangle = _profileRect
        If _hot = 11 Then
            Dim hr As Rectangle = Rectangle.Inflate(pr, 6, 4)
            Using p As GraphicsPath = Theme.RoundRectPath(hr, 26), b As New SolidBrush(Color.FromArgb(250, 243, 236))
                g.FillPath(b, p)
            End Using
        End If
        Avatars.Draw(g, New Rectangle(pr.X, pr.Y + 2, 44, 44), Session.Username, ProfileSvc.MyAvatarPath())
        If Not _compactProfile Then
            TextRenderer.DrawText(g, Session.Username, _fUser, New Rectangle(pr.X + 54, pr.Y + 4, pr.Width - 54, 22), Theme.TextDark,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.VerticalCenter)
            TextRenderer.DrawText(g, "Seller", _fRole, New Rectangle(pr.X + 54, pr.Y + 26, pr.Width - 54, 18), Theme.TextMuted,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
        End If
        Dim fill As Color = If(_isOpen, Theme.SuccessSoft, Theme.DangerSoft)
        Dim fore As Color = If(_isOpen, Theme.Success, Theme.Danger)
        Using p As GraphicsPath = Theme.RoundRectPath(_openRect, 23), b As New SolidBrush(fill)
            g.FillPath(b, p)
        End Using
        Using b As New SolidBrush(fore)
            g.FillEllipse(b, _openRect.X + 20, _openRect.Y + (_openRect.Height - 11) \ 2, 11, 11)
        End Using
        TextRenderer.DrawText(g, If(_isOpen, "Open", "Closed"), _fOpen, New Rectangle(_openRect.X + 38, _openRect.Y, _openRect.Width - 40, _openRect.Height), fore,
                              TextFormatFlags.NoPadding Or TextFormatFlags.VerticalCenter)
    End Sub

    Private Sub DrawMenuIcon(g As Graphics, r As Rectangle, c As Color)
        Using pen As New Pen(c, 2.0F)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawLine(pen, r.X + 2, r.Y + 5, r.Right - 2, r.Y + 5)
            g.DrawLine(pen, r.X + 2, r.Y + 11, r.Right - 2, r.Y + 11)
            g.DrawLine(pen, r.X + 2, r.Y + 17, r.Right - 8, r.Y + 17)
        End Using
    End Sub

    Private Sub DrawLogo(g As Graphics, r As Rectangle)
        If _logo IsNot Nothing Then
            Dim state As GraphicsState = g.Save()
            Using p As New GraphicsPath()
                p.AddEllipse(r)
                g.SetClip(p)
            End Using
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            Dim s As Integer = Math.Min(_logo.Width, _logo.Height)
            Dim src As New Rectangle((_logo.Width - s) \ 2, (_logo.Height - s) \ 2, s, s)
            g.DrawImage(_logo, r, src, GraphicsUnit.Pixel)
            g.Restore(state)
        Else
            Using b As New SolidBrush(Theme.Primary)
                g.FillEllipse(b, r)
            End Using
            Dim initial As String = If(_name.Length > 0, _name.Substring(0, 1).ToUpper(), "?")
            TextRenderer.DrawText(g, initial, _fName, r, Color.White,
                                  TextFormatFlags.NoPadding Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
        End If
    End Sub

    Private Function HitTest(p As Point) As Integer
        LayoutItems()
        For i As Integer = 0 To 5
            If _rects(i).Contains(p) Then Return i
        Next
        If _openRect.Contains(p) Then Return 10
        If Rectangle.Inflate(_profileRect, 6, 4).Contains(p) Then Return 11
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
        If h >= 0 AndAlso h <= 5 Then
            If h <> _active Then RaiseEvent TabClicked(h)
        ElseIf h = 10 Then
            RaiseEvent OpenClicked()
        ElseIf h = 11 Then
            RaiseEvent ProfileClicked()
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
