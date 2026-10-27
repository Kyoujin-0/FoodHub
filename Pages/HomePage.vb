Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Scrollable page that lays itself out from scratch whenever its width changes.</summary>
Public MustInherit Class PageBase
    Inherits Panel

    Protected Const M As Integer = 32
    Private _lastW As Integer = -1
    Private _busy As Boolean
    Private _scrollY As Integer
    Private _contentH As Integer

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As Integer) As Integer
    End Function

    ''' <summary>WS_EX_COMPOSITED: the page and all its children are painted together, so scrolling does not tear.</summary>
    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp As CreateParams = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or &H2000000
            Return cp
        End Get
    End Property

    Public Sub New()
        SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint, True)
        AutoScroll = False
        BackColor = Theme.Bg
        AddHandler ClientSizeChanged, AddressOf OnClientSz
    End Sub

    ''' <summary>Position every control. contentWidth is the usable width between the page margins.</summary>
    Protected MustOverride Sub LayoutPage(contentWidth As Integer)

    Private Function MaxScroll() As Integer
        Return Math.Max(0, _contentH - ClientSize.Height)
    End Function

    Private Sub ScrollTo(y As Integer)
        y = Math.Max(0, Math.Min(MaxScroll(), y))
        Dim d As Integer = y - _scrollY
        If d = 0 Then Return
        If IsHandleCreated Then SendMessage(Handle, &HB, 0, 0)
        SuspendLayout()
        For Each c As Control In Controls
            c.Top = c.Top - d
        Next
        ResumeLayout(False)
        _scrollY = y
        If IsHandleCreated Then
            SendMessage(Handle, &HB, 1, 0)
            Invalidate(True)
        End If
    End Sub

    ''' <summary>Called by WheelFilter. Positive delta = wheel up.</summary>
    Public Sub ScrollByWheel(delta As Integer)
        ScrollTo(_scrollY - CInt(delta / 120.0 * 80))
    End Sub

    Protected Sub RelayoutPage()
        If _busy OrElse ClientSize.Width < 100 Then Return
        _busy = True
        Try
            ScrollTo(0)
            SuspendLayout()
            LayoutPage(Math.Max(560, ClientSize.Width - 2 * M))
            ResumeLayout(False)
        Finally
            _busy = False
        End Try
        _lastW = ClientSize.Width
    End Sub

    ''' <summary>Rebuild the layout but stay at the same scroll position.</summary>
    Protected Sub RelayoutKeepScroll()
        Dim pos As Integer = _scrollY
        RelayoutPage()
        If pos > 0 Then ScrollTo(pos)
    End Sub

    Private Sub OnClientSz(sender As Object, e As EventArgs)
        If ClientSize.Width <> _lastW Then
            RelayoutPage()
        Else
            ScrollTo(_scrollY)
        End If
    End Sub

    Protected Sub SetContentHeight(bottom As Integer)
        _contentH = bottom
        ScrollTo(_scrollY)
    End Sub
End Class

''' <summary>Home: ad banner with search, category chips and the grid of stall cards.</summary>
Public Class HomePage
    Inherits PageBase

    Private ReadOnly _shell As CustomerForm
    Private ReadOnly _banner As New BannerCarousel()
    Private ReadOnly _search As New AppTextBox()
    Private ReadOnly _chips As New ChipBar()
    Private ReadOnly _heading As Label
    Private ReadOnly _count As Label
    Private ReadOnly _empty As Label
    Private ReadOnly _cards As New List(Of StallCard)
    Private ReadOnly _catIds As New List(Of Integer)
    Private ReadOnly _debounce As New Timer()

    Public Sub New(shell As CustomerForm)
        _shell = shell

        _search.LeftIcon = "search"
        _search.PlaceholderText = "Search food stalls or categories"
        _search.Font = Theme.F(11)
        _search.MaxLength = 40
        _banner.Controls.Add(_search)
        AddHandler _banner.SlideClicked, Sub(s, slide) _shell.ShowProducts(slide.StallId)

        _chips.BackColor = Theme.Bg

        _heading = Ui.Lbl("Food stalls", 17, True)
        _count = Ui.Lbl("", 10, False, Theme.TextMuted)
        _empty = Ui.Lbl("No food stalls match your search.", 11, False, Theme.TextMuted)
        _empty.Visible = False

        Controls.Add(_banner)
        Controls.Add(_chips)
        Controls.Add(_heading)
        Controls.Add(_count)
        Controls.Add(_empty)

        _debounce.Interval = 280
        AddHandler _debounce.Tick, Sub(s, e)
                                       _debounce.Stop()
                                       LoadStalls()
                                   End Sub
        AddHandler _search.TextChanged, Sub(s, e)
                                            _debounce.Stop()
                                            _debounce.Start()
                                        End Sub
        AddHandler _search.EnterPressed, Sub(s, e)
                                             _debounce.Stop()
                                             LoadStalls()
                                         End Sub
        AddHandler _chips.SelectedChanged, Sub(s, e) LoadStalls()

        LoadBanners()
        LoadCategories()
        LoadStalls()
    End Sub

    Private Sub LoadBanners()
        Dim slides As New List(Of BannerSlide)
        Dim dt As DataTable = CatalogSvc.GetBanners()
        For Each r As DataRow In dt.Rows
            Dim s As New BannerSlide()
            s.ImagePath = Db.GetStr(r, "image_path")
            s.Title = Db.GetStr(r, "title")
            s.Subtitle = Db.GetStr(r, "stall_name")
            s.StallId = Db.GetInt(r, "stall_id")
            slides.Add(s)
        Next
        _banner.SetSlides(slides)
    End Sub

    Private Sub LoadCategories()
        Dim names As New List(Of String)
        names.Add("All")
        _catIds.Clear()
        _catIds.Add(0)
        Dim dt As DataTable = CatalogSvc.GetStallCategories()
        For Each r As DataRow In dt.Rows
            names.Add(Db.GetStr(r, "name"))
            _catIds.Add(Db.GetInt(r, "id"))
        Next
        _chips.SetItems(names, 0)
    End Sub

    Private Sub LoadStalls()
        Dim catId As Integer = 0
        If _chips.SelectedIndex > 0 AndAlso _chips.SelectedIndex < _catIds.Count Then catId = _catIds(_chips.SelectedIndex)
        Dim dt As DataTable = CatalogSvc.GetStalls(_search.Text, catId)
        For Each c As StallCard In _cards
            Controls.Remove(c)
            c.Dispose()
        Next
        _cards.Clear()
        For Each r As DataRow In dt.Rows
            Dim card As New StallCard(Db.GetInt(r, "id"), Db.GetStr(r, "name"), Db.GetStr(r, "category"), Db.GetBool(r, "is_open"),
                                      Ui.LoadImage(Db.GetStr(r, "cover_path")), Ui.LoadImage(Db.GetStr(r, "logo_path")))
            AddHandler card.Click, Sub(s, e) _shell.ShowProducts(DirectCast(s, StallCard).StallId)
            Controls.Add(card)
            _cards.Add(card)
        Next
        _count.Text = dt.Rows.Count.ToString() & If(dt.Rows.Count = 1, " stall", " stalls")
        _empty.Visible = (dt.Rows.Count = 0)
        RelayoutPage()
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim y As Integer = 24
        _banner.SetBounds(M, y, contentWidth, 270)
        Dim sw As Integer = Math.Min(580, contentWidth - 80)
        _search.SetBounds(CInt((contentWidth - sw) / 2), 26, sw, 52)
        Using gp As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, sw, 52), 14)
            _search.Region = New Region(gp)
        End Using
        y += 270 + 24

        _chips.SetBounds(M, y, contentWidth, 46)
        y += 46 + 20

        _heading.Location = New Point(M, y)
        _count.Location = New Point(M + _heading.PreferredWidth + 12, y + 7)
        y += 44

        _empty.Location = New Point(M, y)

        Dim gap As Integer = 18
        Dim cols As Integer = 5
        While cols > 2 AndAlso (contentWidth - gap * (cols - 1)) / cols < 190
            cols -= 1
        End While
        Dim cardW As Integer = CInt(Math.Floor((contentWidth - gap * (cols - 1)) / cols))
        Dim cardH As Integer = 214
        For i As Integer = 0 To _cards.Count - 1
            Dim col As Integer = i Mod cols
            Dim row As Integer = i \ cols
            _cards(i).SetBounds(M + col * (cardW + gap), y + row * (cardH + gap), cardW, cardH)
        Next
        Dim rows As Integer = CInt(Math.Ceiling(_cards.Count / CDbl(cols)))
        SetContentHeight(y + rows * (cardH + gap) + 24)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _debounce.Stop()
            _debounce.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
