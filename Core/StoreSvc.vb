Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms

''' <summary>Seller Store tab: the stall's own profile (name, category, contact, description, logo, cover) and its sales records.</summary>
Public Module StoreSvc
    ' ====================== profile ======================
    ''' <summary>All profile columns of the logged-in seller's stall. Throws when the description column is missing (run patch_store.sql).</summary>
    Public Function GetStore(stallId As Integer) As DataRow
        Dim dt As DataTable = Db.Query("SELECT id, [name], category_id, contact, [description], logo_path, cover_path, is_open FROM stalls WHERE id=@i AND owner_id=@o",
                                       Db.P("@i", stallId), Db.P("@o", Session.UserId))
        If dt.Rows.Count = 0 Then Return Nothing
        Return dt.Rows(0)
    End Function

    Public Function SaveStore(stallId As Integer, name As String, categoryId As Integer, contact As String, description As String,
                              logoPath As String, coverPath As String, ByRef errMsg As String) As Boolean
        Try
            Dim taken As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM stalls WHERE [name]=@n AND id<>@i", Db.P("@n", name), Db.P("@i", stallId)))
            If taken > 0 Then
                errMsg = "That stall name is already used by another stall."
                Return False
            End If
            Db.Exec("UPDATE stalls SET [name]=@n, category_id=@c, contact=@t, [description]=@d, logo_path=@l, cover_path=@v WHERE id=@i AND owner_id=@o",
                    Db.P("@n", name), Db.P("@c", Db.NullIfZero(categoryId)), Db.P("@t", contact),
                    Db.P("@d", If(If(description, "") = "", Nothing, description)),
                    Db.P("@l", If(If(logoPath, "") = "", Nothing, logoPath)),
                    Db.P("@v", If(If(coverPath, "") = "", Nothing, coverPath)),
                    Db.P("@i", stallId), Db.P("@o", Session.UserId))
            Return True
        Catch ex As Exception
            errMsg = "Could not save the store: " & ex.Message
            Return False
        End Try
    End Function

    ' ====================== photos ======================
    ''' <summary>Stores a logo (square, max 512 px) or cover (max 1400 px) as JPEG in images\stalls. Returns the relative path, or "" with errMsg.</summary>
    Public Function SaveStallImage(sourceFile As String, isLogo As Boolean, ByRef errMsg As String) As String
        Try
            If New FileInfo(sourceFile).Length > InboxSvc.MaxImageBytes Then
                errMsg = "That photo is too big (max 20 MB)."
                Return ""
            End If
            Using src As Image = InboxSvc.LoadImgFile(sourceFile)
                If src Is Nothing Then
                    errMsg = "That file is not a valid image."
                    Return ""
                End If
                Dim bmp As Bitmap
                If isLogo Then
                    Dim side As Integer = Math.Min(src.Width, src.Height)
                    Dim target As Integer = Math.Min(512, side)
                    bmp = New Bitmap(target, target)
                    Using g As Graphics = Graphics.FromImage(bmp)
                        g.Clear(Color.White)
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic
                        g.DrawImage(src, New Rectangle(0, 0, target, target), New Rectangle((src.Width - side) \ 2, (src.Height - side) \ 2, side, side), GraphicsUnit.Pixel)
                    End Using
                Else
                    Dim sc As Double = Math.Min(1.0, 1400.0 / Math.Max(src.Width, src.Height))
                    Dim w As Integer = Math.Max(1, CInt(src.Width * sc))
                    Dim h As Integer = Math.Max(1, CInt(src.Height * sc))
                    bmp = New Bitmap(w, h)
                    Using g As Graphics = Graphics.FromImage(bmp)
                        g.Clear(Color.White)
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic
                        g.DrawImage(src, 0, 0, w, h)
                    End Using
                End If
                Using bmp
                    Dim dir As String = Path.Combine(Db.DataFolder(), "images", "stalls")
                    Directory.CreateDirectory(dir)
                    Dim fname As String = If(isLogo, "logo_", "cover_") & Guid.NewGuid().ToString("N") & ".jpg"
                    bmp.Save(Path.Combine(dir, fname), ImageFormat.Jpeg)
                    Return "images/stalls/" & fname
                End Using
            End Using
        Catch ex As Exception
            errMsg = "Could not save the photo: " & ex.Message
            Return ""
        End Try
    End Function

    ''' <summary>Deletes a stall photo file, but only files this page created (images/stalls/...).</summary>
    Public Sub DeleteStallImage(relPath As String)
        Try
            If String.IsNullOrEmpty(relPath) OrElse Not relPath.StartsWith("images/stalls/") Then Return
            Dim full As String = Path.Combine(Db.DataFolder(), relPath.Replace("/", "\"))
            If File.Exists(full) Then File.Delete(full)
        Catch
        End Try
    End Sub

    ' ====================== sales records (completed orders only) ======================
    Private Const SalesFilter As String = "os.stall_id=@s AND os.[status]='completed' AND IIf(os.completed_at IS NULL, os.updated_at, os.completed_at) >= @from"

    ''' <summary>One row: orders, revenue, items.</summary>
    Public Function GetSalesTotals(stallId As Integer, fromDate As DateTime) As DataRow
        Dim t As DataTable = Db.Query("SELECT COUNT(*) AS n, SUM(os.subtotal) AS revenue FROM order_stalls os WHERE " & SalesFilter, Db.P("@s", stallId), Db.P("@from", fromDate))
        Dim it As DataTable = Db.Query("SELECT SUM(i.quantity) AS q FROM order_items i INNER JOIN order_stalls os ON os.id=i.order_stall_id WHERE " & SalesFilter, Db.P("@s", stallId), Db.P("@from", fromDate))
        Dim dt As New DataTable()
        dt.Columns.Add("orders", GetType(Integer))
        dt.Columns.Add("revenue", GetType(Decimal))
        dt.Columns.Add("items", GetType(Integer))
        Dim row As DataRow = dt.NewRow()
        row("orders") = Db.GetInt(t.Rows(0), "n")
        row("revenue") = Db.GetDec(t.Rows(0), "revenue")
        row("items") = Db.GetInt(it.Rows(0), "q")
        dt.Rows.Add(row)
        Return dt.Rows(0)
    End Function

    Public Function GetTopItems(stallId As Integer, fromDate As DateTime) As DataTable
        Return Db.Query("SELECT TOP 5 i.product_name, SUM(i.quantity) AS qty, SUM((i.unit_price + i.extras_total) * i.quantity) AS revenue " &
                        "FROM order_items i INNER JOIN order_stalls os ON os.id=i.order_stall_id WHERE " & SalesFilter & " " &
                        "GROUP BY i.product_name ORDER BY SUM(i.quantity) DESC, SUM((i.unit_price + i.extras_total) * i.quantity) DESC", Db.P("@s", stallId), Db.P("@from", fromDate))
    End Function

    Public Function GetSalesOrders(stallId As Integer, fromDate As DateTime) As DataTable
        Dim dt As DataTable = Db.Query("SELECT TOP 1000 os.id, os.order_id, u.username, os.subtotal, IIf(os.completed_at IS NULL, os.updated_at, os.completed_at) AS done_at " &
                        "FROM (order_stalls os INNER JOIN orders o ON o.id=os.order_id) INNER JOIN users u ON u.id=o.customer_id WHERE " & SalesFilter & " " &
                        "ORDER BY IIf(os.completed_at IS NULL, os.updated_at, os.completed_at) DESC, os.id DESC", Db.P("@s", stallId), Db.P("@from", fromDate))
        Db.AttachJoinedText(dt, "id", "items",
                            "SELECT i.order_stall_id AS k, (i.quantity & ' x ' & i.product_name) AS t FROM order_items i WHERE i.order_stall_id IN ({IDS}) ORDER BY i.id", "k", "t")
        Return dt
    End Function
End Module
