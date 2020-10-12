# Change Tracking (CT) Kullanarak Sql Server ve Couchbase Arasında Veri Eşitlemek
Sql Server'da bir tablo üzerindeki Insert, Update, Delete yani DML olayları CDT opsiyonu ile takip edilebilir.

SqlServer bu konuda Change Tracking (CT) ve Change Data Capturing (CDC) olmak üzere iki özellik sağlar.

> Her iki özelliğin [detaylarını](https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/track-data-changes-sql-server?view=sql-server-ver15) incelemelisiniz. Bu proje içindeki kullanım sizin sisteminizin çalışma şekkline uymayabilir ve değişiklikleri doğru bir şekilde aktaramayabilirsiniz.

## Uyarı
SQL Server bir talodaki DML değişikliklerinin CT ile doğru takip edilebilmesi için **Snapshot Isolation** kullanmayı yoğun olarak tavsiye eder. [Burayı](https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/work-with-change-tracking-sql-server?view=sql-server-ver15) inceleyiniz. Bu projedeki senaryo için aşağıdaki varsayımlar yapıldı ve **Snapshot Isolation** kullanılmadı.

- Değişiklik kayıtları SQL Server tarafından silinmeden önce okunur ve Couchbase'e eşitlenir. *Sql Server'da CHANGE_RETENTION ayarını inceleyin.*
- Saklanan *cdtLastTrackingVersion* değeri *CHANGE_TRACKING_MIN_VALID_VERSION()* değerinden küçük olmaması sağlanır. Bazı yönetimsel aktiviteler sonucunda *CHANGE_TRACKING_MIN_VALID_VERSION()* değeri Couchbase'de saklanan *cdtLastTrackingVersion* değerinden küçük hale gelebilir. Buna yol açacak yönetimsel aktivitelerden sonra Couchbase'de saklanan *cdtLastTrackingVersion* değeri silinmelidir. SQL Server dokümanlarını inceleyerek buna yol açabilecek aktiviteleri belirleyin.

Aşağıdaki hazırlıklardan sonra uygulama çalışmaya hazır hale gelir. Bu adımları tamamladıktan sonra uygulamayı çalıştırın.

## Kullanılan Harici Paketler
Projede kullanılan paketleri *nuget* kaynağından ekleyin.

    dotnet add package CouchbaseNetClient
    dotnet add package System.Data.SqlClient
    dotnet add Package Dapper

## Bir Couchbase Örneiğini Docker ile Çalıştırma
    docker run -d -p 8091-8094:8091-8094 -p 11210:11210 couchbase

## Bir Sql Server Örneğini Docker ile Çalıştırma
> Geliştirme ortamınızda çalışan bir SQL sunucu var ise **1433** portu kullanımda olabilir. Çakışma olmaması için geliştirme ortamında çalışan SQL sunucusunu durdurun.

    docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=yourStrong(!)Password' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2019-latest

SQL Server'daki kurulumları yapmak için **Managment Studio** ile yeni çalıştırılan örneğe bağlanabilirsiniz. Sunucu: localhost, Kullanıcı adı: sa, Şifre: yourStrong(!)Password olarak kullanın.

## SQL Server'da Örnek Tablo ve Verinin Oluşturulması
Aşağıdaki script ile örnek ortamı oluşturun.

    CREATE DATABASE SQLToCouchbaseSample;
    GO
    USE SQLToCouchbaseSample;
    GO
    ALTER DATABASE SQLToCouchbaseSample SET CHANGE_TRACKING = ON (CHANGE_RETENTION = 3 DAYS, AUTO_CLEANUP = ON);
    GO
    CREATE TABLE Product (
        ProductId INT PRIMARY KEY IDENTITY (1, 1),
        ProductName NVARCHAR (255) NOT NULL,
        BrandId INT NOT NULL,
        CategoryId INT NOT NULL,
        ModelYear INT NOT NULL,
        ListPrice DECIMAL (10, 2) NOT NULL
    );
    GO
    ALTER TABLE Product ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = OFF);
    GO
    DECLARE @Index INT = 1;
    WHILE @Index <= 1000 BEGIN
        INSERT INTO Product (ProductName, BrandId, CategoryId, ModelYear, ListPrice)
        SELECT 
            'Product :' + CAST(@Index AS VARCHAR(100))
            ,FLOOR(RAND()*(100-50+1)+50)
            ,FLOOR(RAND()*(10-5+1)+5)
            ,FLOOR(RAND()*(2000-2020+1)+2020)
            ,FLOOR(RAND()*(100-1+1)+1) + RAND();
        SET @Index = @Index + 1;
    END;
    GO

## Couchbase'de Cluster ve Bucket Oluşturma
Tarayıcınız ile localhost:8091 adresine gidin. **Setup New Cluster** adımını seçin ve Couchbase'de yeni bir cluster oluşturmak için gerekli adımları uygulayın. Kullanıcı adını Administrator ve şifreyi 111111 olarak ayarlayın. Cluster adı olarak dilediğiniz bir isim seçin. Kullanıcı sözleşmesini kabul ettikten sonra **Finish With Defaults** ile kurulumu tamamlayın.

**Buckets** menüsünden **ADD BUCKET** seçeneği ile yeni bir bucket oluşturun. İsim olarak *documents* atayın ve diğer seçenekleri olduğu gibi bırakın.

Bu hazırlıklardan sonra uygulama çalıştırılabilir. Bir terminal açın ve proje kök dizininde aşağıdaki komutu çalıştırın.

    dotnet run

Eşitleme ile ilgili mesajları gördükten sonra Couchbase panelinden *documents* isimli bucket içindeki dokümanları inceleyebilirsiniz. Couchbase paneline localhost:8091 yolu ile ulaşabiliriniz.

**Managment Studio** yardımı ile aşağıdaki cümleyi çalıştırın.

    UPDATE Product SET ProductName = N'1 numaralı ürünün adı değişti' WHERE ProductId = 1;

Couchbase panelinden *Product-1* isimli dokümanın *ProductName* özelliğine bakın. *1 numaralı ürünün adı değişti* olarak gücellendiğini görün.