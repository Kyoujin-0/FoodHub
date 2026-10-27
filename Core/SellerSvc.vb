Imports System.Data
Imports System.Data.OleDb

''' <summary>A size / variant of an item (Id 0 = new).</summary>
Public Class SizeRow
    Public Id As Integer
    Public Name As String = ""
    Public Price As Decimal
End Class

''' <summary>An extra in the stall's extras library (Id 0 = new).</summary>
Public Class ExtraRow
    Public Id As Integer
    Public Name As String = ""
    Public Price As Decimal
End Class

''' <summary>Everything the seller entered for one menu item.</summary>
Public Class ProductDraft
    Public Id As Integer
    Public StallId As Integer
    Public Name As String = ""
    Public Description As String = ""
    Public Category As String = ""
    Public ImagePath As String = ""
    Public Price As Decimal
    Public MaxPerOrder As Integer          ' 0 = no limit
    Public TrackStock As Boolean
    Public Stock As Integer
    Public LowStockAt As Integer = 10      ' shows as Low when stock <= this
    Public ReadOnly Sizes As New List(Of SizeRow)
    Public ReadOnly ExtraIds As New List(Of Integer)       ' stall_extras ids this item offers
    Public ReadOnly Removables As New List(Of String)
End Class

''' <summary>Seller-side data access: own stall, order board, menu.</summary>
Public Module SellerSvc
    ' ====================== stall ======================
    ''' <summary>The stall owned by the logged-in seller (Nothing if none).</summary>
    Public Function GetMyStall() As DataRow
        Dim dt As DataTable = Db.Query("SELECT TOP 1 id, [name], logo_path, is_open FROM stalls WHERE owner_id=@o", Db.P("@o", Session.UserId))
        If dt.Rows.Count = 0 Then Return Nothing
        Return dt.Rows(0)
    End Function

    Public Sub SetOpen(stallId As Integer, isOpen As Boolean)
        Db.Exec("UPDATE stalls SET is_open=@v WHERE id=@s AND owner_id=@o",
                Db.P("@v", If(isOpen, 1, 0)), Db.P("@s", stallId), Db.P("@o", Session.UserId))
    End Sub

    ' ====================== order board ======================
    ''' <summary>Active orders of a stall (pending / preparing / ready) in queue order: oldest change first.</summary>
    Public Function GetBoard(stallId As Integer) As DataTable
        Return Db.Query("SELECT os.id, os.order_id, os.[status], os.subtotal, o.created_at, u.username " &
                        "FROM (order_stalls os INNER JOIN orders o ON o.id=os.order_id) INNER JOIN users u ON u.id=o.customer_id " &
                        "WHERE os.stall_id=@s AND os.[status] IN ('pending','preparing','ready') " &
                        "ORDER BY os.updated_at, os.id", Db.P("@s", stallId))
    End Function

    ''' <summary>Moves an order from one status to the next. Returns False if it was already changed (stale card).
    ''' When declining (cancelled) the optional reason is saved and also sent to the customer as a message.
    ''' updated_at and completed_at are set here (they used to be filled by MySQL: ON UPDATE and a trigger).</summary>
    Public Function SetStatus(orderStallId As Integer, stallId As Integer, fromStatus As String, toStatus As String, reason As String) As Boolean
        reason = If(reason, "").Trim()
        Dim saved As String = Nothing
        If toStatus = "cancelled" AndAlso reason <> "" Then saved = reason
        Dim stamp As DateTime = DateTime.Now
        Dim finished As Boolean = (toStatus = "completed" OrElse toStatus = "cancelled")
        Dim n As Integer = Db.Exec("UPDATE order_stalls SET [status]=@t, decline_reason=@r, updated_at=@u, completed_at=@c WHERE id=@i AND stall_id=@s AND [status]=@f",
                                   Db.P("@t", toStatus), Db.P("@r", saved), Db.P("@u", stamp), Db.P("@c", If(finished, CObj(stamp), Nothing)),
                                   Db.P("@i", orderStallId), Db.P("@s", stallId), Db.P("@f", fromStatus))
        If n = 0 Then Return False
        If toStatus = "cancelled" Then
            Try
                RestoreStock(orderStallId)
            Catch
            End Try
        End If
        If toStatus = "cancelled" Then
            Dim body As String = "Sorry, your order was declined."
            If reason <> "" Then body &= " Reason: " & reason
            OrderSvc.SendMessage(orderStallId, body)
        End If
        Return True
    End Function

    ' ====================== menu: categories ======================
    Public Function GetMenuCategories(stallId As Integer) As DataTable
        Return Db.Query("SELECT id, [name] FROM menu_categories WHERE stall_id=@s ORDER BY [name]", Db.P("@s", stallId))
    End Function

    ''' <summary>Finds the stall's menu category with that name or creates it. Returns its id (0 when the name is empty: the item stays uncategorized).</summary>
    Public Function AddMenuCategory(stallId As Integer, name As String) As Integer
        name = If(name, "").Trim()
        If name = "" Then Return 0
        Dim o As Object = Db.Scalar("SELECT TOP 1 id FROM menu_categories WHERE stall_id=@s AND [name]=@n", Db.P("@s", stallId), Db.P("@n", name))
        If o IsNot Nothing AndAlso Not IsDBNull(o) Then Return Convert.ToInt32(o)
        Return CInt(Db.Insert("INSERT INTO menu_categories (stall_id, [name]) VALUES (@s,@n)", Db.P("@s", stallId), Db.P("@n", name)))
    End Function

    ' ====================== menu: items ======================
    Public Function GetMenuItems(stallId As Integer) As DataTable
        Return Db.Query("SELECT p.id, p.[name], p.image_path, p.base_price, p.is_available, p.track_stock, p.stock, p.max_per_order, " &
                        "IIf(mc.[name] IS NULL, 'Uncategorized', mc.[name]) AS category, mc.id AS category_id, " &
                        "IIf((SELECT COUNT(*) FROM product_sizes z WHERE z.product_id=p.id)>0, (SELECT MIN(z.price) FROM product_sizes z WHERE z.product_id=p.id), p.base_price) AS from_price, " &
                        "(SELECT COUNT(*) FROM product_sizes z WHERE z.product_id=p.id) AS size_count " &
                        "FROM products p LEFT JOIN menu_categories mc ON mc.id=p.menu_category_id " &
                        "WHERE p.stall_id=@s ORDER BY mc.[name], p.[name]", Db.P("@s", stallId))
    End Function

    Public Function GetProduct(productId As Integer, stallId As Integer) As DataRow
        Dim dt As DataTable = Db.Query("SELECT p.id, p.[name], p.short_desc, p.[description], p.image_path, p.base_price, p.is_available, p.max_per_order, p.track_stock, p.stock, p.low_stock_at, " &
                                       "IIf(mc.[name] IS NULL, '', mc.[name]) AS category FROM products p LEFT JOIN menu_categories mc ON mc.id=p.menu_category_id " &
                                       "WHERE p.id=@i AND p.stall_id=@s", Db.P("@i", productId), Db.P("@s", stallId))
        If dt.Rows.Count = 0 Then Return Nothing
        Return dt.Rows(0)
    End Function

    Public Function GetLinkedExtraIds(productId As Integer) As List(Of Integer)
        Dim l As New List(Of Integer)
        Dim dt As DataTable = Db.Query("SELECT stall_extra_id FROM product_extras WHERE product_id=@p AND stall_extra_id IS NOT NULL", Db.P("@p", productId))
        For Each r As DataRow In dt.Rows
            l.Add(Db.GetInt(r, "stall_extra_id"))
        Next
        Return l
    End Function

    Public Function GetRemovables(productId As Integer) As List(Of String)
        Dim l As New List(Of String)
        Dim dt As DataTable = Db.Query("SELECT [name] FROM product_removables WHERE product_id=@p ORDER BY id", Db.P("@p", productId))
        For Each r As DataRow In dt.Rows
            l.Add(Db.GetStr(r, "name"))
        Next
        Return l
    End Function

    Public Sub SetAvailable(productId As Integer, stallId As Integer, isAvailable As Boolean)
        Db.Exec("UPDATE products SET is_available=@v WHERE id=@i AND stall_id=@s", Db.P("@v", If(isAvailable, 1, 0)), Db.P("@i", productId), Db.P("@s", stallId))
    End Sub

    ''' <summary>Deletes an item with everything that hangs on it. Access has no cascade here, so the children go first;
    ''' past orders keep their snapshot (order_items.product_id becomes empty).</summary>
    Public Function DeleteProduct(productId As Integer, stallId As Integer) As Boolean
        Dim own As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM products WHERE id=@i AND stall_id=@s", Db.P("@i", productId), Db.P("@s", stallId)))
        If own = 0 Then Return False
        Using cn As OleDbConnection = Db.Open()
            Using tx As OleDbTransaction = cn.BeginTransaction()
                Try
                    Db.ExecOn(cn, tx, "UPDATE order_items SET product_id=NULL WHERE product_id=@p", Db.P("@p", productId))
                    Db.ExecOn(cn, tx, "DELETE FROM cart_item_extras WHERE cart_item_id IN (SELECT id FROM cart_items WHERE product_id=@p)", Db.P("@p", productId))
                    Db.ExecOn(cn, tx, "DELETE FROM cart_item_extras WHERE extra_id IN (SELECT id FROM product_extras WHERE product_id=@p)", Db.P("@p", productId))
                    Db.ExecOn(cn, tx, "DELETE FROM cart_items WHERE product_id=@p", Db.P("@p", productId))
                    Db.ExecOn(cn, tx, "DELETE FROM reviews WHERE product_id=@p", Db.P("@p", productId))
                    Db.ExecOn(cn, tx, "DELETE FROM product_extras WHERE product_id=@p", Db.P("@p", productId))
                    Db.ExecOn(cn, tx, "DELETE FROM product_sizes WHERE product_id=@p", Db.P("@p", productId))
                    Db.ExecOn(cn, tx, "DELETE FROM product_removables WHERE product_id=@p", Db.P("@p", productId))
                    Db.ExecOn(cn, tx, "DELETE FROM products WHERE id=@p AND stall_id=@s", Db.P("@p", productId), Db.P("@s", stallId))
                    tx.Commit()
                Catch
                    tx.Rollback()
                    Throw
                End Try
            End Using
        End Using
        Return True
    End Function

    ''' <summary>Creates or updates an item with its variants, extras and removable ingredients in one transaction.</summary>
    Public Function SaveProduct(d As ProductDraft, ByRef newId As Integer, ByRef errMsg As String) As Boolean
        Try
            Dim catId As Integer = AddMenuCategory(d.StallId, d.Category)
            Dim oldSizeIds As New List(Of Integer)
            Dim oldLinks As New Dictionary(Of Integer, Integer)   ' stall_extra_id -> product_extras.id
            If d.Id > 0 Then
                Dim own As Object = Db.Scalar("SELECT COUNT(*) FROM products WHERE id=@i AND stall_id=@s", Db.P("@i", d.Id), Db.P("@s", d.StallId))
                If Convert.ToInt32(own) = 0 Then
                    errMsg = "That item no longer exists."
                    Return False
                End If
                For Each r As DataRow In Db.Query("SELECT id FROM product_sizes WHERE product_id=@p", Db.P("@p", d.Id)).Rows
                    oldSizeIds.Add(Db.GetInt(r, "id"))
                Next
                For Each r As DataRow In Db.Query("SELECT id, stall_extra_id FROM product_extras WHERE product_id=@p AND stall_extra_id IS NOT NULL", Db.P("@p", d.Id)).Rows
                    oldLinks(Db.GetInt(r, "stall_extra_id")) = Db.GetInt(r, "id")
                Next
            End If
            Dim extraLib As DataTable = Db.Query("SELECT id, [name], price FROM stall_extras WHERE stall_id=@s", Db.P("@s", d.StallId))

            Dim basePrice As Decimal = d.Price
            If d.Sizes.Count > 0 Then
                basePrice = d.Sizes(0).Price
                For Each z As SizeRow In d.Sizes
                    If z.Price < basePrice Then basePrice = z.Price
                Next
            End If
            Dim shortDesc As String = If(d.Description = "", Nothing, Ui.Ellipsize(d.Description, 100))
            Dim longDesc As String = If(d.Description = "", Nothing, d.Description)
            Dim img As String = If(d.ImagePath = "", Nothing, d.ImagePath)

            ' availability rule (was a CASE in MySQL): a tracked item with 0 stock is unavailable; restocking from 0 makes it available again
            Dim availExpr As String = "is_available"
            If d.TrackStock Then availExpr = If(d.Stock = 0, "0", "IIf(stock=0, 1, is_available)")

            Using cn As OleDbConnection = Db.Open()
                Using tx As OleDbTransaction = cn.BeginTransaction()
                    Try
                        Dim pid As Long = d.Id
                        If d.Id = 0 Then
                            pid = Db.ExecOn(cn, tx, "INSERT INTO products (stall_id, menu_category_id, [name], short_desc, [description], image_path, base_price, is_available, max_per_order, track_stock, stock, low_stock_at) " &
                                            "VALUES (@s,@m,@n,@sd,@d,@img,@bp,@av,@mx,@ts,@st,@lo)",
                                            Db.P("@s", d.StallId), Db.P("@m", Db.NullIfZero(catId)), Db.P("@n", d.Name), Db.P("@sd", shortDesc), Db.P("@d", longDesc), Db.P("@img", img),
                                            Db.P("@bp", basePrice), Db.P("@av", If(d.TrackStock AndAlso d.Stock = 0, 0, 1)),
                                            Db.P("@mx", Db.NullIfZero(d.MaxPerOrder)), Db.P("@ts", If(d.TrackStock, 1, 0)), Db.P("@st", d.Stock), Db.P("@lo", d.LowStockAt))
                        Else
                            Db.ExecOn(cn, tx, "UPDATE products SET is_available=" & availExpr & ", " &
                                      "menu_category_id=@m, [name]=@n, short_desc=@sd, [description]=@d, image_path=@img, base_price=@bp, max_per_order=@mx, track_stock=@ts, stock=@st, low_stock_at=@lo " &
                                      "WHERE id=@i AND stall_id=@s",
                                      Db.P("@m", Db.NullIfZero(catId)), Db.P("@n", d.Name), Db.P("@sd", shortDesc), Db.P("@d", longDesc), Db.P("@img", img), Db.P("@bp", basePrice),
                                      Db.P("@mx", Db.NullIfZero(d.MaxPerOrder)), Db.P("@ts", If(d.TrackStock, 1, 0)), Db.P("@st", d.Stock), Db.P("@lo", d.LowStockAt),
                                      Db.P("@i", d.Id), Db.P("@s", d.StallId))
                        End If

                        ' variants (update in place so customers' saved sizes survive)
                        Dim keepSizes As New HashSet(Of Integer)
                        For Each z As SizeRow In d.Sizes
                            If z.Id > 0 AndAlso oldSizeIds.Contains(z.Id) Then
                                Db.ExecOn(cn, tx, "UPDATE product_sizes SET [name]=@n, price=@p WHERE id=@i AND product_id=@pid",
                                          Db.P("@n", z.Name), Db.P("@p", z.Price), Db.P("@i", z.Id), Db.P("@pid", pid))
                                keepSizes.Add(z.Id)
                            Else
                                Db.ExecOn(cn, tx, "INSERT INTO product_sizes (product_id, [name], price) VALUES (@pid,@n,@p)",
                                          Db.P("@pid", pid), Db.P("@n", z.Name), Db.P("@p", z.Price))
                            End If
                        Next
                        For Each oid As Integer In oldSizeIds
                            If Not keepSizes.Contains(oid) Then
                                ' saved cart lines that used this size lose the size (MySQL did this with ON DELETE SET NULL)
                                Db.ExecOn(cn, tx, "UPDATE cart_items SET size_id=NULL WHERE size_id=@i", Db.P("@i", oid))
                                Db.ExecOn(cn, tx, "DELETE FROM product_sizes WHERE id=@i AND product_id=@pid", Db.P("@i", oid), Db.P("@pid", pid))
                            End If
                        Next

                        ' extras: link the chosen stall extras to this item
                        Dim keepLinks As New HashSet(Of Integer)
                        For Each sid As Integer In d.ExtraIds
                            Dim info As DataRow = Nothing
                            For Each r As DataRow In extraLib.Rows
                                If Db.GetInt(r, "id") = sid Then info = r
                            Next
                            If info Is Nothing Then Continue For
                            keepLinks.Add(sid)
                            If oldLinks.ContainsKey(sid) Then
                                Db.ExecOn(cn, tx, "UPDATE product_extras SET [name]=@n, price=@p WHERE id=@i",
                                          Db.P("@n", Db.GetStr(info, "name")), Db.P("@p", Db.GetDec(info, "price")), Db.P("@i", oldLinks(sid)))
                            Else
                                Db.ExecOn(cn, tx, "INSERT INTO product_extras (product_id, [name], price, stall_extra_id) VALUES (@pid,@n,@p,@se)",
                                          Db.P("@pid", pid), Db.P("@n", Db.GetStr(info, "name")), Db.P("@p", Db.GetDec(info, "price")), Db.P("@se", sid))
                            End If
                        Next
                        For Each kv As KeyValuePair(Of Integer, Integer) In oldLinks
                            If Not keepLinks.Contains(kv.Key) Then
                                Db.ExecOn(cn, tx, "DELETE FROM cart_item_extras WHERE extra_id=@i", Db.P("@i", kv.Value))
                                Db.ExecOn(cn, tx, "DELETE FROM product_extras WHERE id=@i", Db.P("@i", kv.Value))
                            End If
                        Next

                        ' removable ingredients
                        Db.ExecOn(cn, tx, "DELETE FROM product_removables WHERE product_id=@pid", Db.P("@pid", pid))
                        For Each nm As String In d.Removables
                            Db.ExecOn(cn, tx, "INSERT INTO product_removables (product_id, [name]) VALUES (@pid,@n)", Db.P("@pid", pid), Db.P("@n", nm))
                        Next

                        tx.Commit()
                        newId = CInt(pid)
                    Catch
                        tx.Rollback()
                        Throw
                    End Try
                End Using
            End Using
            Return True
        Catch ex As Exception
            errMsg = "Could not save the item: " & ex.Message
            Return False
        End Try
    End Function

    ' ====================== inventory ======================
    ''' <summary>Every item of the stall with its stock settings (tracked and not tracked).</summary>
    Public Function GetInventory(stallId As Integer) As DataTable
        Return Db.Query("SELECT p.id, p.[name], p.image_path, p.is_available, p.track_stock, p.stock, p.low_stock_at, IIf(mc.[name] IS NULL, 'Uncategorized', mc.[name]) AS category " &
                        "FROM products p LEFT JOIN menu_categories mc ON mc.id=p.menu_category_id WHERE p.stall_id=@s ORDER BY p.[name]", Db.P("@s", stallId))
    End Function

    ''' <summary>Adds delta (can be negative) to the stock. Reaching 0 marks the item unavailable; restocking from 0 makes it available again.</summary>
    Public Sub AdjustStock(productId As Integer, stallId As Integer, delta As Integer)
        Db.Exec("UPDATE products SET is_available = IIf(IIf(stock+@d<0, 0, stock+@d)=0, 0, IIf(stock=0, 1, is_available)), " &
                "stock = IIf(stock+@d<0, 0, stock+@d) WHERE id=@i AND stall_id=@s AND track_stock=1",
                Db.P("@d", delta), Db.P("@i", productId), Db.P("@s", stallId))
    End Sub

    ''' <summary>Sets the stock to an exact number (same availability rule as AdjustStock).</summary>
    Public Sub SetStock(productId As Integer, stallId As Integer, newStock As Integer)
        Dim n As Integer = Math.Max(0, newStock)
        Dim availExpr As String = If(n = 0, "0", "IIf(stock=0, 1, is_available)")
        Db.Exec("UPDATE products SET is_available = " & availExpr & ", stock = @n WHERE id=@i AND stall_id=@s AND track_stock=1",
                Db.P("@n", n), Db.P("@i", productId), Db.P("@s", stallId))
    End Sub

    ''' <summary>Turns stock tracking on for an item with a starting quantity and its low-stock alert level.</summary>
    Public Sub StartTracking(productId As Integer, stallId As Integer, qty As Integer, lowAt As Integer)
        Dim n As Integer = Math.Max(0, qty)
        Db.Exec("UPDATE products SET track_stock=1, is_available=@a, stock=@n, low_stock_at=@lo WHERE id=@i AND stall_id=@s",
                Db.P("@a", If(n = 0, 0, 1)), Db.P("@n", n), Db.P("@lo", Math.Max(0, lowAt)), Db.P("@i", productId), Db.P("@s", stallId))
    End Sub

    ''' <summary>Puts back the stock of a declined order (only items that track stock).</summary>
    Private Sub RestoreStock(orderStallId As Integer)
        Dim lines As DataTable = Db.Query("SELECT product_id, SUM(quantity) AS q FROM order_items WHERE order_stall_id=@os AND product_id IS NOT NULL GROUP BY product_id", Db.P("@os", orderStallId))
        For Each r As DataRow In lines.Rows
            Db.Exec("UPDATE products SET is_available = IIf(stock=0, 1, is_available), stock = stock + @q WHERE id=@p AND track_stock=1",
                    Db.P("@q", Db.GetInt(r, "q")), Db.P("@p", Db.GetInt(r, "product_id")))
        Next
    End Sub

    ' ====================== menu: stall extras library ======================
    ''' <summary>Old items have extras that are not in the library yet: copy them over once (safe to run many times).</summary>
    Public Sub EnsureExtrasLibrary(stallId As Integer)
        Dim loose As DataTable = Db.Query("SELECT e.[name] AS nm, MIN(e.price) AS pr FROM product_extras e INNER JOIN products p ON p.id=e.product_id " &
                                          "WHERE p.stall_id=@s AND e.stall_extra_id IS NULL GROUP BY e.[name]", Db.P("@s", stallId))
        If loose.Rows.Count = 0 Then Return
        For Each r As DataRow In loose.Rows
            Dim nm As String = Db.GetStr(r, "nm")
            Dim have As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM stall_extras WHERE stall_id=@s AND [name]=@n", Db.P("@s", stallId), Db.P("@n", nm)))
            If have = 0 Then
                Db.Exec("INSERT INTO stall_extras (stall_id, [name], price) VALUES (@s,@n,@p)", Db.P("@s", stallId), Db.P("@n", nm), Db.P("@p", Db.GetDec(r, "pr")))
            End If
        Next
        Dim libRows As DataTable = Db.Query("SELECT id, [name] FROM stall_extras WHERE stall_id=@s", Db.P("@s", stallId))
        Dim ids As DataTable = Db.Query("SELECT e.id, e.[name] FROM product_extras e INNER JOIN products p ON p.id=e.product_id WHERE p.stall_id=@s AND e.stall_extra_id IS NULL", Db.P("@s", stallId))
        For Each er As DataRow In ids.Rows
            For Each lr As DataRow In libRows.Rows
                If String.Equals(Db.GetStr(lr, "name"), Db.GetStr(er, "name"), StringComparison.OrdinalIgnoreCase) Then
                    Db.Exec("UPDATE product_extras SET stall_extra_id=@se WHERE id=@i", Db.P("@se", Db.GetInt(lr, "id")), Db.P("@i", Db.GetInt(er, "id")))
                    Exit For
                End If
            Next
        Next
    End Sub

    Public Function GetStallExtras(stallId As Integer) As DataTable
        Return Db.Query("SELECT id, [name], price FROM stall_extras WHERE stall_id=@s ORDER BY [name]", Db.P("@s", stallId))
    End Function

    ''' <summary>Saves the whole extras library: updates, inserts, and deletes the ones that are no longer listed.
    ''' Items that use an extra get the new name / price automatically; deleted extras disappear from every item.</summary>
    Public Function SaveStallExtras(stallId As Integer, rows As List(Of ExtraRow), ByRef errMsg As String) As Boolean
        Try
            Dim existing As DataTable = Db.Query("SELECT id FROM stall_extras WHERE stall_id=@s", Db.P("@s", stallId))
            Dim keep As New HashSet(Of Integer)
            For Each r As ExtraRow In rows
                If r.Id > 0 Then keep.Add(r.Id)
            Next
            Using cn As OleDbConnection = Db.Open()
                Using tx As OleDbTransaction = cn.BeginTransaction()
                    Try
                        For Each er As DataRow In existing.Rows
                            Dim id As Integer = Db.GetInt(er, "id")
                            If Not keep.Contains(id) Then
                                ' remove the extra from every item first (MySQL did this with ON DELETE CASCADE)
                                Db.ExecOn(cn, tx, "DELETE FROM cart_item_extras WHERE extra_id IN (SELECT id FROM product_extras WHERE stall_extra_id=@i)", Db.P("@i", id))
                                Db.ExecOn(cn, tx, "DELETE FROM product_extras WHERE stall_extra_id=@i", Db.P("@i", id))
                                Db.ExecOn(cn, tx, "DELETE FROM stall_extras WHERE id=@i AND stall_id=@s", Db.P("@i", id), Db.P("@s", stallId))
                            End If
                        Next
                        For Each r As ExtraRow In rows
                            If r.Id > 0 Then
                                Db.ExecOn(cn, tx, "UPDATE stall_extras SET [name]=@n, price=@p WHERE id=@i AND stall_id=@s",
                                          Db.P("@n", r.Name), Db.P("@p", r.Price), Db.P("@i", r.Id), Db.P("@s", stallId))
                                ' items that use this extra follow its new name / price
                                Db.ExecOn(cn, tx, "UPDATE product_extras SET [name]=@n, price=@p WHERE stall_extra_id=@i",
                                          Db.P("@n", r.Name), Db.P("@p", r.Price), Db.P("@i", r.Id))
                            Else
                                Db.ExecOn(cn, tx, "INSERT INTO stall_extras (stall_id, [name], price) VALUES (@s,@n,@p)",
                                          Db.P("@s", stallId), Db.P("@n", r.Name), Db.P("@p", r.Price))
                            End If
                        Next
                        tx.Commit()
                    Catch
                        tx.Rollback()
                        Throw
                    End Try
                End Using
            End Using
            Return True
        Catch ex As Exception
            errMsg = "Could not save the extras: " & ex.Message
            Return False
        End Try
    End Function
End Module
