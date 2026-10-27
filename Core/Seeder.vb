Imports System.Data
Imports System.Data.OleDb
Imports System.Globalization

''' <summary>
''' First-run sample data so the app can be demoed right away.
''' Logins created:  admin / Admin123   customer / Customer1   (sellers: manang, lolaaida, brewchill, burgerbarn, sweetspot, noodlenook  all with password Seller1)
''' </summary>
Public Module Seeder
    Private ReadOnly _productIds As New List(Of Long)

    ''' <summary>The stall categories are fixed lookup data: make sure they exist (one row each, never duplicated).</summary>
    Private Sub EnsureCategories()
        For Each nm As String In {"Filipino", "Chicken", "Rice Meals", "Drinks", "Snacks", "Desserts", "Noodles", "Burgers"}
            Dim have As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM stall_categories WHERE [name]=@n", Db.P("@n", nm)))
            If have = 0 Then Db.Exec("INSERT INTO stall_categories ([name]) VALUES (@n)", Db.P("@n", nm))
        Next
    End Sub

    Public Sub EnsureSeeded()
        EnsureCategories()
        Dim admins As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM users WHERE [role]='admin'"))
        If admins > 0 Then Return

        MakeUser("admin", "Admin123", "admin", "active")
        MakeUser("customer", "Customer1", "customer", "active")

        ' cheap dummy reviewers (cannot log in)
        Dim dummyHash As String = BCrypt.Net.BCrypt.HashPassword("Dummy123", 10)
        Dim reviewers As New List(Of Long)
        For i As Integer = 1 To 12
            reviewers.Add(Db.Insert("INSERT INTO users (username, password_hash, [role], [status], created_at) VALUES (@u,@p,'customer','disabled',@t)", Db.P("@u", "guest" & i.ToString("00")), Db.P("@p", dummyHash), Db.P("@t", DateTime.Now)))
        Next

        Dim s1 As Long = SeedStall("manang", "Manang's Fried Chicken", "Chicken", "09171234567", True)
        AddProd(s1, "Chicken", "Crispy Fried Chicken", "Golden, juicy and extra crispy.", "Our signature fried chicken, marinated overnight and fried to a golden crunch. Served hot with your choice of size.", 89, "1 pc:89|2 pcs:165|Bucket (6 pcs):469", "Gravy:10|Extra Rice:20")
        AddProd(s1, "Chicken", "Spicy Buffalo Wings", "Fiery wings tossed in buffalo sauce.", "Crispy wings tossed in a tangy, spicy buffalo sauce. Comes with a cooling dip on the side.", 149, "6 pcs:149|12 pcs:279", "Cheese Dip:15|Ranch Dip:15")
        AddProd(s1, "Chicken", "Chicken Sandwich", "Crunchy chicken fillet in a toasted bun.", "Crispy chicken fillet, lettuce and house mayo in a toasted brioche bun.", 99, "", "Cheese:15|Bacon:25")
        AddProd(s1, "Sides", "Seasoned Fries", "Crispy fries with your choice of seasoning.", "Thick-cut fries, fried until golden and tossed in seasoning.", 59, "Medium:59|Large:79", "Cheese Dust:10|Sour Cream:15")
        AddProd(s1, "Sides", "Creamy Coleslaw", "Cool and crunchy side salad.", "Fresh shredded cabbage and carrots in a creamy dressing.", 39, "", "")
        AddProd(s1, "Drinks", "House Iced Tea", "Sweet, chilled and refreshing.", "Freshly brewed black tea with a hint of lemon, served over ice.", 39, "Medium:39|Large:49", "")

        Dim s2 As Long = SeedStall("lolaaida", "Lola Aida's Kitchen", "Filipino", "09181234567", True)
        AddProd(s2, "Rice Meals", "Chicken Adobo Rice Meal", "Classic soy-vinegar adobo with rice.", "Tender chicken slow-cooked in soy sauce, vinegar and garlic. Served with steaming rice.", 119, "", "Fried Egg:15|Extra Rice:20")
        AddProd(s2, "Rice Meals", "Lechon Kawali Rice Meal", "Crispy pork belly with liver sauce.", "Deep-fried pork belly with a shatteringly crisp skin, served with rice and liver sauce.", 139, "", "Fried Egg:15|Atchara:10")
        AddProd(s2, "Rice Meals", "Tapsilog", "Beef tapa, garlic rice and egg.", "Sweet-savory beef tapa served with garlic fried rice and a sunny-side-up egg.", 109, "", "Extra Rice:20|Extra Egg:15")
        AddProd(s2, "Soups", "Pork Sinigang", "Sour tamarind soup with pork and veggies.", "Pork ribs simmered with tamarind, kangkong, radish and tomatoes.", 129, "Regular:129|Large:189", "Extra Rice:20")
        AddProd(s2, "Noodles", "Pancit Canton", "Stir-fried noodles with veggies and pork.", "Egg noodles stir-fried with vegetables, pork and a splash of calamansi.", 79, "Solo:79|Family:249", "Calamansi:5")
        AddProd(s2, "Drinks", "Sago't Gulaman", "Brown sugar drink with sago and gulaman.", "A classic Filipino cooler with arnibal, sago pearls and gulaman jelly.", 35, "Medium:35|Large:45", "")

        Dim s3 As Long = SeedStall("brewchill", "Brew & Chill", "Drinks", "09191234567", True)
        AddProd(s3, "Coffee", "Iced Americano", "Bold espresso over ice.", "Double-shot espresso poured over ice and cold water. Clean, bold and refreshing.", 75, "Medium:75|Large:95", "Extra Shot:30|Oat Milk:25")
        AddProd(s3, "Coffee", "Caramel Macchiato", "Espresso, milk and caramel drizzle.", "Layers of steamed milk, espresso and buttery caramel.", 105, "Medium:105|Large:125", "Extra Shot:30|Whipped Cream:20")
        AddProd(s3, "Milk Tea", "Classic Milk Tea", "Creamy black milk tea.", "Our house black tea blended with creamy milk. Customize with your favorite toppings.", 85, "Medium:85|Large:105", "Pearls:15|Pudding:20|Cheese Foam:30")
        AddProd(s3, "Milk Tea", "Wintermelon Milk Tea", "Sweet, mellow wintermelon.", "Wintermelon syrup with creamy milk tea. A crowd favorite.", 85, "Medium:85|Large:105", "Pearls:15|Pudding:20")
        AddProd(s3, "Fruit Drinks", "Fresh Lemonade", "Squeezed to order.", "Fresh lemons, simple syrup and ice. Tart and sweet.", 59, "Medium:59|Large:79", "Mint:10")
        AddProd(s3, "Fruit Drinks", "Mango Shake", "Thick and creamy ripe mango.", "Ripe mangoes blended with milk and ice.", 89, "Medium:89|Large:109", "Graham Crumbs:10")

        Dim s4 As Long = SeedStall("burgerbarn", "Burger Barn", "Burgers", "09201234567", True)
        AddProd(s4, "Burgers", "Classic Cheeseburger", "Beef patty, cheese and house sauce.", "Juicy beef patty, melted cheese, pickles and our house sauce on a toasted bun.", 99, "", "Extra Patty:45|Bacon:25|Fried Egg:15")
        AddProd(s4, "Burgers", "Double Bacon Burger", "Two patties stacked with bacon.", "Two beef patties, crispy bacon, cheddar and caramelized onions.", 159, "", "Extra Patty:45|Fried Egg:15")
        AddProd(s4, "Burgers", "Hotdog Sandwich", "Jumbo hotdog in a soft bun.", "Grilled jumbo hotdog with onions, ketchup and mustard.", 69, "", "Cheese:15")
        AddProd(s4, "Sides", "Crinkle Fries", "Crispy crinkle-cut fries.", "Crinkle-cut fries fried golden and lightly salted.", 59, "Medium:59|Large:79", "Cheese Sauce:15")
        AddProd(s4, "Sides", "Onion Rings", "Beer-battered onion rings.", "Thick onion rings in a crunchy batter, with dipping sauce.", 69, "", "")
        AddProd(s4, "Drinks", "Cola Float", "Cola topped with vanilla ice cream.", "Ice-cold cola with a scoop of creamy vanilla ice cream.", 65, "Medium:65|Large:85", "")

        Dim s5 As Long = SeedStall("sweetspot", "Sweet Spot", "Desserts", "09211234567", False)
        AddProd(s5, "Cold Treats", "Halo-Halo", "Mixed shaved-ice dessert.", "Shaved ice, sweet beans, fruits, leche flan and ube ice cream, drizzled with milk.", 89, "Regular:89|Special:129", "Extra Ube Ice Cream:25|Extra Leche Flan:20")
        AddProd(s5, "Cold Treats", "Leche Flan", "Silky caramel custard.", "A smooth, creamy caramel custard made from egg yolks and milk.", 59, "", "")
        AddProd(s5, "Cakes", "Ube Cake Slice", "Soft ube chiffon with cream.", "A slice of fluffy ube chiffon cake with whipped cream frosting.", 85, "", "")
        AddProd(s5, "Cakes", "Turon", "Banana spring rolls with caramel.", "Saba banana and jackfruit wrapped in crisp lumpia wrapper, caramelized in brown sugar.", 35, "1 pc:35|3 pcs:95", "")

        Dim s6 As Long = SeedStall("noodlenook", "Noodle Nook", "Noodles", "09221234567", True)
        AddProd(s6, "Noodles", "Beef Mami", "Hot beef noodle soup.", "Egg noodles in a rich beef broth topped with tender beef slices and spring onion.", 99, "Regular:99|Large:129", "Egg:15|Extra Noodles:25")
        AddProd(s6, "Noodles", "Tonkotsu Ramen", "Creamy pork-bone broth ramen.", "Slow-simmered pork-bone broth with ramen noodles, chashu, egg and nori.", 189, "", "Extra Chashu:50|Egg:20|Extra Noodles:30")
        AddProd(s6, "Noodles", "Pancit Palabok", "Shrimpy sauce over rice noodles.", "Rice noodles with a golden shrimp sauce, chicharon, egg and green onions.", 99, "", "Extra Sauce:15")
        AddProd(s6, "Noodles", "Special Lomi", "Thick noodles in a hearty broth.", "Thick egg noodles in a savory, thick broth with pork, liver and egg.", 109, "", "Egg:15")
        AddProd(s6, "Rice Bowls", "Chicken Teriyaki Bowl", "Glazed chicken over steamed rice.", "Grilled chicken thigh glazed in sweet teriyaki sauce, over rice with sesame and greens.", 129, "", "Egg:15|Extra Rice:20")

        ' ad banners (the admin will manage these later)
        Db.Exec("INSERT INTO banners (title, stall_id, sort_order, is_active) VALUES (@t,@s,1,1)", Db.P("@t", "Crispy deals every day"), Db.P("@s", s1))
        Db.Exec("INSERT INTO banners (title, stall_id, sort_order, is_active) VALUES (@t,@s,2,1)", Db.P("@t", "Brew your day right"), Db.P("@s", s3))
        Db.Exec("INSERT INTO banners (title, stall_id, sort_order, is_active) VALUES (@t,@s,3,1)", Db.P("@t", "Home-cooked favorites"), Db.P("@s", s2))
        Db.Exec("INSERT INTO banners (title, stall_id, sort_order, is_active) VALUES (@t,@s,4,1)", Db.P("@t", "Stacked burgers, no limits"), Db.P("@s", s4))

        ' reviews
        Dim rnd As New Random(42)
        Dim stars As Integer() = {3, 4, 4, 4, 5, 5, 5, 5}
        Dim comments As String() = {"Sulit and so tasty!", "Fresh and hot, would order again.", "Good portion for the price.", "Fast service, great flavor.", "My go-to every lunch break.", "Pretty good!", "Loved it."}
        ' one transaction for all the reviews (much faster in Access than one commit per row)
        Using cn As OleDbConnection = Db.Open()
            Using tx As OleDbTransaction = cn.BeginTransaction()
                Try
                    For Each pid As Long In _productIds
                        For Each uid As Long In reviewers
                            If rnd.NextDouble() < 0.55 Then
                                Db.ExecOn(cn, tx, "INSERT INTO reviews (product_id, customer_id, rating, [comment], created_at) VALUES (@p,@u,@r,@c,@t)",
                                          Db.P("@p", pid), Db.P("@u", uid), Db.P("@r", stars(rnd.Next(stars.Length))), Db.P("@c", comments(rnd.Next(comments.Length))),
                                          Db.P("@t", DateTime.Now.AddDays(-rnd.Next(0, 30)).AddMinutes(-rnd.Next(0, 1440))))
                            End If
                        Next
                    Next
                    tx.Commit()
                Catch
                    tx.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Sub

    Private Function MakeUser(name As String, pw As String, role As String, status As String) As Long
        Return Db.Insert("INSERT INTO users (username, password_hash, [role], [status], created_at) VALUES (@u,@p,@r,@s,@t)",
                         Db.P("@u", name), Db.P("@p", BCrypt.Net.BCrypt.HashPassword(pw, 10)), Db.P("@r", role), Db.P("@s", status), Db.P("@t", DateTime.Now))
    End Function

    Private Function SeedStall(username As String, stallName As String, category As String, contact As String, isOpen As Boolean) As Long
        Dim uid As Long = MakeUser(username, "Seller1", "seller", "active")
        Dim catId As Integer = Convert.ToInt32(Db.Scalar("SELECT id FROM stall_categories WHERE [name]=@n", Db.P("@n", category)))
        Return Db.Insert("INSERT INTO stalls (owner_id, [name], category_id, contact, is_open, rent_due_date) VALUES (@o,@n,@c,@p,@i,@d)",
                         Db.P("@o", uid), Db.P("@n", stallName), Db.P("@c", catId), Db.P("@p", contact), Db.P("@i", If(isOpen, 1, 0)), Db.P("@d", DateTime.Today.AddMonths(1)))
    End Function

    Private Function MenuCatId(stallId As Long, name As String) As Long
        Dim o As Object = Db.Scalar("SELECT id FROM menu_categories WHERE stall_id=@s AND [name]=@n", Db.P("@s", stallId), Db.P("@n", name))
        If o IsNot Nothing AndAlso Not IsDBNull(o) Then Return Convert.ToInt64(o)
        Return Db.Insert("INSERT INTO menu_categories (stall_id, [name]) VALUES (@s,@n)", Db.P("@s", stallId), Db.P("@n", name))
    End Function

    Private Sub AddProd(stallId As Long, menuCat As String, name As String, shortDesc As String, longDesc As String, price As Decimal, sizes As String, extras As String)
        Dim mc As Long = MenuCatId(stallId, menuCat)
        Dim pid As Long = Db.Insert("INSERT INTO products (stall_id, menu_category_id, [name], short_desc, [description], base_price, is_available, track_stock, stock, low_stock_at) VALUES (@s,@m,@n,@sd,@d,@p,1,0,0,10)",
                                    Db.P("@s", stallId), Db.P("@m", mc), Db.P("@n", name), Db.P("@sd", shortDesc), Db.P("@d", longDesc), Db.P("@p", price))
        _productIds.Add(pid)
        If sizes <> "" Then
            For Each part As String In sizes.Split("|"c)
                Dim kv As String() = part.Split(":"c)
                Db.Exec("INSERT INTO product_sizes (product_id, [name], price) VALUES (@p,@n,@pr)", Db.P("@p", pid), Db.P("@n", kv(0)), Db.P("@pr", Decimal.Parse(kv(1), CultureInfo.InvariantCulture)))
            Next
        End If
        If extras <> "" Then
            For Each part As String In extras.Split("|"c)
                Dim kv As String() = part.Split(":"c)
                Db.Exec("INSERT INTO product_extras (product_id, [name], price) VALUES (@p,@n,@pr)", Db.P("@p", pid), Db.P("@n", kv(0)), Db.P("@pr", Decimal.Parse(kv(1), CultureInfo.InvariantCulture)))
            Next
        End If
    End Sub
End Module
