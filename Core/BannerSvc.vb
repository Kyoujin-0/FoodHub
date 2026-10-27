Imports System.Data
Imports System.Data.OleDb
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms

''' <summary>One banner of the customer Home carousel, as the admin edits it.</summary>
Public Class BannerRow
    Public Id As Integer
    ''' <summary>Name only the admin sees ("Isles Banner").</summary>
    Public Title As String = ""
    ''' <summary>Big text drawn on the banner on the customer Home. Different for every banner.</summary>
    Public Headline As String = ""
    ''' <summary>Relative path like images/banners/banner_123.jpg (or absolute). Empty = no photo.</summary>
    Public ImagePath As String = ""
    ''' <summary>Stall that opens when the banner is clicked. 0 = no link.</summary>
    Public StallId As Integer
    Public StallName As String = ""
    Public SortOrder As Integer
    Public IsActive As Boolean = True
    ''' <summary>Photo position: 0 = show the left/top part of the photo, 1 = the right/bottom part, 0.5 = centred.</summary>
    Public FocusX As Double = 0.5
    Public FocusY As Double = 0.5
    ''' <summary>1 = photo just covers the banner, 3 = three times bigger.</summary>
    Public Zoom As Double = 1.0
End Class

''' <summary>One entry of the "opens stall" drop-down.</summary>
Public Class StallChoice
    Public Id As Integer
    Public Name As String = ""
    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class

''' <summary>Banner data (Access) and photo files. Uses its own small OleDb helpers so it does not depend on other modules.</summary>
Public Module BannerSvc
    ''' <summary>The table definition. Core/AccessSchema.vb uses this for new databases; EnsureSchema upgrades old ones.</summary>
    Public Const CreateSql As String = _
        "CREATE TABLE banners (id COUNTER PRIMARY KEY, image_path TEXT(255), title TEXT(80), headline TEXT(40), stall_id LONG, " &
        "sort_order LONG NOT NULL, is_active BYTE NOT NULL, img_x DOUBLE, img_y DOUBLE, img_zoom DOUBLE)"
    Private Const FkSql As String = _
        "ALTER TABLE banners ADD CONSTRAINT fk_banners_stall_id FOREIGN KEY (stall_id) REFERENCES stalls (id)"
    Public Const MaxHeadline As Integer = 40
    Public Const MaxTitle As Integer = 40
    Private Const PhotoFolder As String = "images/banners/"

    Private _ready As Boolean
    Private ReadOnly _imgCache As New Dictionary(Of String, Image)

    ' ---------------- tiny OleDb helpers ----------------
    Private Function OpenDb() As OleDbConnection
        Dim cn As OleDbConnection = Db.Open()
        If cn.State <> ConnectionState.Open Then cn.Open()
        Return cn
    End Function

    Private Function MakeCmd(cn As OleDbConnection, sql As String, args As Object()) As OleDbCommand
        Dim c As New OleDbCommand(sql, cn)
        If args IsNot Nothing Then
            For Each a As Object In args
                If a Is Nothing Then
                    c.Parameters.AddWithValue("?", DBNull.Value)
                Else
                    c.Parameters.AddWithValue("?", a)
                End If
            Next
        End If
        Return c
    End Function

    Private Sub DoExec(sql As String, ParamArray args As Object())
        Using cn As OleDbConnection = OpenDb()
            Using c As OleDbCommand = MakeCmd(cn, sql, args)
                c.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Private Function DoQuery(sql As String, ParamArray args As Object()) As DataTable
        Dim dt As New DataTable()
        Using cn As OleDbConnection = OpenDb()
            Using c As OleDbCommand = MakeCmd(cn, sql, args)
                Using da As New OleDbDataAdapter(c)
                    da.Fill(dt)
                End Using
            End Using
        End Using
        Return dt
    End Function

    Private Function StrV(r As DataRow, col As String) As String
        If r.IsNull(col) Then Return ""
        Return Convert.ToString(r(col))
    End Function

    Private Function IntV(r As DataRow, col As String) As Integer
        If r.IsNull(col) Then Return 0
        Return Convert.ToInt32(r(col))
    End Function

    Private Function DblV(r As DataRow, col As String, def As Double) As Double
        If r.IsNull(col) Then Return def
        Return Convert.ToDouble(r(col))
    End Function

    Private Function NullIfEmpty(s As String) As Object
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Return s.Trim()
    End Function

    ' ---------------- schema upgrade ----------------
    ''' <summary>
    ''' Old databases have a banners table without headline / photo position and with a required stall.
    ''' The first time the Banners tab (or the customer Home) needs it, the table is rebuilt and the old rows are kept
    ''' (their title becomes the headline). New databases already have the new table, so nothing happens.
    ''' </summary>
    Public Sub EnsureSchema()
        If _ready Then Return
        Dim hasHeadline As Boolean
        Using cn As OleDbConnection = OpenDb()
            Using dt As DataTable = cn.GetSchema("Columns", New String() {Nothing, Nothing, "banners", "headline"})
                hasHeadline = dt.Rows.Count > 0
            End Using
        End Using
        If Not hasHeadline Then Rebuild()
        _ready = True
    End Sub

    Private Sub Rebuild()
        Dim old As DataTable = DoQuery("SELECT id, image_path, title, stall_id, sort_order, is_active FROM banners ORDER BY sort_order, id")
        Try
            DoExec("ALTER TABLE banners DROP CONSTRAINT fk_banners_stall_id")
        Catch
        End Try
        DoExec("DROP TABLE banners")
        DoExec(CreateSql)
        Try
            DoExec(FkSql)
        Catch
        End Try
        Dim ord As Integer = 0
        For Each r As DataRow In old.Rows
            Dim title As String = StrV(r, "title")
            Dim stall As Object = Nothing
            If IntV(r, "stall_id") > 0 Then stall = IntV(r, "stall_id")
            DoExec("INSERT INTO banners (image_path, title, headline, stall_id, sort_order, is_active, img_x, img_y, img_zoom) VALUES (?, ?, ?, ?, ?, ?, 0.5, 0.5, 1)",
                   NullIfEmpty(StrV(r, "image_path")), NullIfEmpty(title), NullIfEmpty(title), stall, ord, CByte(If(IntV(r, "is_active") <> 0, 1, 0)))
            ord += 1
        Next
    End Sub

    ' ---------------- reading ----------------
    Public Function GetAll() As List(Of BannerRow)
        EnsureSchema()
        Dim dt As DataTable = DoQuery(
            "SELECT b.id, b.image_path, b.title, b.headline, b.stall_id, b.sort_order, b.is_active, b.img_x, b.img_y, b.img_zoom, s.[name] AS stall_name " &
            "FROM banners AS b LEFT JOIN stalls AS s ON b.stall_id = s.id ORDER BY b.sort_order, b.id")
        Dim list As New List(Of BannerRow)
        For Each r As DataRow In dt.Rows
            Dim b As New BannerRow()
            b.Id = IntV(r, "id")
            b.ImagePath = StrV(r, "image_path")
            b.Title = StrV(r, "title")
            b.Headline = StrV(r, "headline")
            b.StallId = IntV(r, "stall_id")
            b.StallName = StrV(r, "stall_name")
            b.SortOrder = IntV(r, "sort_order")
            b.IsActive = IntV(r, "is_active") <> 0
            b.FocusX = DblV(r, "img_x", 0.5)
            b.FocusY = DblV(r, "img_y", 0.5)
            b.Zoom = Math.Max(1.0, DblV(r, "img_zoom", 1.0))
            If b.StallName = "" Then b.StallId = 0
            list.Add(b)
        Next
        Return list
    End Function

    ''' <summary>The stalls the admin can pick for "opens stall".</summary>
    Public Function GetStalls() As List(Of StallChoice)
        Dim dt As DataTable = DoQuery("SELECT id, [name] FROM stalls ORDER BY [name]")
        Dim list As New List(Of StallChoice)
        For Each r As DataRow In dt.Rows
            Dim s As New StallChoice()
            s.Id = IntV(r, "id")
            s.Name = StrV(r, "name")
            list.Add(s)
        Next
        Return list
    End Function

    ''' <summary>What the customer Home shows: active banners in the admin's order.</summary>
    Public Function GetSlides() As List(Of BannerSlide)
        Dim slides As New List(Of BannerSlide)
        For Each b As BannerRow In GetAll()
            If Not b.IsActive Then Continue For
            Dim s As New BannerSlide()
            s.ImagePath = b.ImagePath
            s.Title = b.Title
            s.Headline = b.Headline
            s.Subtitle = ""
            s.StallId = b.StallId
            s.FocusX = b.FocusX
            s.FocusY = b.FocusY
            s.Zoom = b.Zoom
            slides.Add(s)
        Next
        Return slides
    End Function

    ' ---------------- writing ----------------
    ''' <summary>Adds (Id = 0) or updates a banner. New banners go to the end of the list.</summary>
    Public Function Save(b As BannerRow, ByRef err As String) As Boolean
        Try
            EnsureSchema()
            Dim stall As Object = Nothing
            If b.StallId > 0 Then stall = b.StallId
            Dim active As Byte = CByte(If(b.IsActive, 1, 0))
            Dim title As String = If(String.IsNullOrWhiteSpace(b.Title), "Untitled banner", b.Title.Trim())
            If b.Id = 0 Then
                Dim mx As Object
                Using cn As OleDbConnection = OpenDb()
                    Using c As OleDbCommand = MakeCmd(cn, "SELECT MAX(sort_order) FROM banners", Nothing)
                        mx = c.ExecuteScalar()
                    End Using
                End Using
                Dim nextOrder As Integer = 0
                If mx IsNot Nothing AndAlso Not IsDBNull(mx) Then nextOrder = Convert.ToInt32(mx) + 1
                DoExec("INSERT INTO banners (image_path, title, headline, stall_id, sort_order, is_active, img_x, img_y, img_zoom) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                       NullIfEmpty(b.ImagePath), title, NullIfEmpty(b.Headline), stall, nextOrder, active, b.FocusX, b.FocusY, b.Zoom)
            Else
                DoExec("UPDATE banners SET image_path = ?, title = ?, headline = ?, stall_id = ?, is_active = ?, img_x = ?, img_y = ?, img_zoom = ? WHERE id = ?",
                       NullIfEmpty(b.ImagePath), title, NullIfEmpty(b.Headline), stall, active, b.FocusX, b.FocusY, b.Zoom, b.Id)
            End If
            Return True
        Catch ex As Exception
            err = ex.Message
            Return False
        End Try
    End Function

    Public Function Delete(id As Integer, ByRef err As String) As Boolean
        Try
            EnsureSchema()
            DoExec("DELETE FROM banners WHERE id = ?", id)
            Return True
        Catch ex As Exception
            err = ex.Message
            Return False
        End Try
    End Function

    ''' <summary>Saves a new order: ids in the order they should be shown.</summary>
    Public Function Reorder(ids As List(Of Integer), ByRef err As String) As Boolean
        Try
            EnsureSchema()
            For i As Integer = 0 To ids.Count - 1
                DoExec("UPDATE banners SET sort_order = ? WHERE id = ?", i, ids(i))
            Next
            Return True
        Catch ex As Exception
            err = ex.Message
            Return False
        End Try
    End Function

    ' ---------------- photos ----------------
    ''' <summary>Full path of a stored photo path (relative paths live next to the database).</summary>
    Public Function FullPath(p As String) As String
        If String.IsNullOrEmpty(p) Then Return ""
        If Path.IsPathRooted(p) Then Return p
        Dim rel As String = p.Replace("/"c, Path.DirectorySeparatorChar)
        Dim a As String = Path.Combine(Db.DataFolder(), rel)
        If File.Exists(a) Then Return a
        Dim b As String = Path.Combine(Application.StartupPath, rel)
        If File.Exists(b) Then Return b
        Return a
    End Function

    ''' <summary>Photo for painting (cached, shared: never Dispose it). Nothing when missing.</summary>
    Public Function LoadImage(p As String) As Image
        If String.IsNullOrEmpty(p) Then Return Nothing
        Dim img As Image = Nothing
        If _imgCache.TryGetValue(p, img) Then Return img
        Try
            Dim f As String = FullPath(p)
            If Not File.Exists(f) Then Return Nothing
            img = LoadFile(f, 1800)
            _imgCache(p) = img
            Return img
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>Opens any picture file without locking it, shrunk to maxWidth. The caller owns (and disposes) the result.</summary>
    Public Function LoadFile(fileName As String, maxWidth As Integer) As Image
        Using fs As New FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Using src As Image = Image.FromStream(fs)
                Dim scale As Double = Math.Min(1.0, maxWidth / CDbl(Math.Max(1, src.Width)))
                Dim w As Integer = Math.Max(1, CInt(src.Width * scale))
                Dim h As Integer = Math.Max(1, CInt(src.Height * scale))
                Dim bmp As New Bitmap(w, h)
                Using g As Graphics = Graphics.FromImage(bmp)
                    g.Clear(Color.White)
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    g.DrawImage(src, 0, 0, w, h)
                End Using
                Return bmp
            End Using
        End Using
    End Function

    ''' <summary>Stores a picked picture as a JPEG (max 1800 px wide) in images/banners and returns the path to save in the database.</summary>
    Public Function SaveImage(sourceFile As String) As String
        Dim folder As String = Path.Combine(Db.DataFolder(), "images", "banners")
        Directory.CreateDirectory(folder)
        Dim name As String = "banner_" & DateTime.Now.Ticks.ToString() & ".jpg"
        Using bmp As Image = LoadFile(sourceFile, 1800)
            Dim codec As ImageCodecInfo = Nothing
            For Each c As ImageCodecInfo In ImageCodecInfo.GetImageEncoders()
                If c.MimeType = "image/jpeg" Then codec = c
            Next
            If codec Is Nothing Then
                bmp.Save(Path.Combine(folder, name), ImageFormat.Jpeg)
            Else
                Using ep As New EncoderParameters(1)
                    ep.Param(0) = New EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L)
                    bmp.Save(Path.Combine(folder, name), codec, ep)
                End Using
            End If
        End Using
        Return PhotoFolder & name
    End Function

    ''' <summary>Deletes a photo file that this tab created (only inside images/banners).</summary>
    Public Sub DeleteImageFile(p As String)
        Try
            If String.IsNullOrEmpty(p) Then Return
            If Not p.Replace("\"c, "/"c).StartsWith(PhotoFolder, StringComparison.OrdinalIgnoreCase) Then Return
            _imgCache.Remove(p)
            Dim f As String = FullPath(p)
            If File.Exists(f) Then File.Delete(f)
        Catch
        End Try
    End Sub
End Module

''' <summary>
''' Draws a banner. Used by the customer carousel, the admin preview, the list thumbnails and the photo-position box,
''' so all of them look the same. Layout: photo, cream fade over the left part, headline on the left, optional "Order now" pill.
''' </summary>
Public Module BannerPaint
    ''' <summary>Width / height used by the admin previews (the customer carousel is about this shape).</summary>
    Public Const AspectRatio As Double = 4.5

    Private ReadOnly Cream As Color = Color.FromArgb(255, 247, 238)

    ''' <summary>Where the photo is drawn inside the banner rectangle for a given position (fx, fy) and zoom.</summary>
    Public Function ImageRect(r As Rectangle, img As Image, fx As Double, fy As Double, zoom As Double) As Rectangle
        If zoom < 1.0 Then zoom = 1.0
        fx = Math.Max(0.0, Math.Min(1.0, fx))
        fy = Math.Max(0.0, Math.Min(1.0, fy))
        Dim scale As Double = Math.Max(r.Width / CDbl(img.Width), r.Height / CDbl(img.Height)) * zoom
        Dim dw As Integer = CInt(Math.Ceiling(img.Width * scale))
        Dim dh As Integer = CInt(Math.Ceiling(img.Height * scale))
        Dim x As Integer = r.X + CInt((r.Width - dw) * fx)
        Dim y As Integer = r.Y + CInt((r.Height - dh) * fy)
        Return New Rectangle(x, y, dw, dh)
    End Function

    Public Sub Draw(g As Graphics, r As Rectangle, img As Image, fx As Double, fy As Double, zoom As Double,
                    headline As String, seed As String, showButton As Boolean, showSafe As Boolean, radius As Integer)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path As GraphicsPath = Theme.RoundRectPath(r, radius)
            Dim saved As GraphicsState = g.Save()
            g.SetClip(path, CombineMode.Intersect)
            If img IsNot Nothing Then
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                Dim ir As Rectangle = ImageRect(r, img, fx, fy, zoom)
                g.DrawImage(img, ir.X, ir.Y, ir.Width, ir.Height)
            Else
                Dim cols As Color() = Theme.PlaceholderColors(If(seed, "banner"))
                Using lg As New LinearGradientBrush(r, cols(0), cols(1), 20.0F)
                    g.FillRectangle(lg, r)
                End Using
                Using br As New SolidBrush(Color.FromArgb(28, 255, 255, 255))
                    g.FillEllipse(br, r.Right - r.Height, r.Y - r.Height \ 3, r.Height * 3 \ 2, r.Height * 3 \ 2)
                End Using
            End If

            ' cream fade: solid on the far left, gone before the middle of the right half
            Dim fade As New Rectangle(r.X, r.Y, Math.Max(2, CInt(r.Width * 0.66)), r.Height)
            Using lb As New LinearGradientBrush(fade, Color.FromArgb(255, Cream), Color.FromArgb(0, Cream), LinearGradientMode.Horizontal)
                Dim cb As New ColorBlend(3)
                cb.Positions = New Single() {0.0F, 0.45F, 1.0F}
                cb.Colors = New Color() {Color.FromArgb(255, Cream), Color.FromArgb(205, Cream), Color.FromArgb(0, Cream)}
                lb.InterpolationColors = cb
                g.FillRectangle(lb, fade)
            End Using
            g.Restore(saved)
        End Using

        If Not String.IsNullOrEmpty(headline) Then
            Dim pt As Single = CSng(Math.Max(13, Math.Round(r.Height * 0.105)))
            Dim tx As Integer = r.X + Math.Max(24, CInt(r.Width * 0.04))
            Dim ty As Integer = r.Y + CInt(r.Height * 0.1)
            Dim tw As Integer = Math.Max(60, CInt(r.Width * 0.46))
            Dim th As Integer = Math.Max(30, CInt(r.Height * 0.58) - CInt(r.Height * 0.1))
            TextRenderer.DrawText(g, headline, Theme.F(pt, True), New Rectangle(tx, ty, tw, th), Theme.TextDark,
                                  TextFormatFlags.Left Or TextFormatFlags.Bottom Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or TextFormatFlags.EndEllipsis)
        End If

        If showButton AndAlso r.Height >= 120 Then
            Dim bw As Integer = 112
            Dim brc As New Rectangle(r.Right - bw - 28, r.Bottom - 62, bw, 34)
            Using bp As GraphicsPath = Theme.RoundRectPath(brc, 17), bb As New SolidBrush(Color.White)
                g.FillPath(bb, bp)
            End Using
            TextRenderer.DrawText(g, "Order now", Theme.F(9.5F, True), brc, Theme.PrimaryDark,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End If

        If showSafe Then
            Dim sr As New Rectangle(r.X + CInt(r.Width * 0.5), r.Y + CInt(r.Height * 0.1), CInt(r.Width * 0.46), CInt(r.Height * 0.8))
            Using fillB As New SolidBrush(Color.FromArgb(34, 255, 107, 53)), pen As New Pen(Color.FromArgb(230, 255, 255, 255), 2.0F)
                pen.DashStyle = DashStyle.Dash
                g.FillRectangle(fillB, sr)
                g.DrawRectangle(pen, sr)
            End Using
            TextRenderer.DrawText(g, "Safe area: keep key content here", Theme.F(9.5F, True), New Rectangle(sr.X + 10, sr.Y + 6, sr.Width - 20, 22), Color.White,
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
        End If
    End Sub
End Module
