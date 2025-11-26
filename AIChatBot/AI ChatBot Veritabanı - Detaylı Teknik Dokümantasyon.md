# 📘 AI ChatBot Veritabanı - Detaylı Teknik Dokümantasyon

## 📑 İçindekiler

1. [Veritabanı Genel Bakış](#1-veritabanı-genel-bakış)
2. [Tablo Yapıları](#2-tablo-yapıları)
3. [İlişkiler ve Kısıtlamalar](#3-i̇lişkiler-ve-kısıtlamalar)
4. [Stored Procedures](#4-stored-procedures)
5. [İndeksler ve Performans](#5-i̇ndeksler-ve-performans)
6. [Veri Akışı Senaryoları](#6-veri-akışı-senaryoları)
7. [Bakım ve Optimizasyon](#7-bakım-ve-optimizasyon)

---

## 1. Veritabanı Genel Bakış

### 🎯 Amaç
**AIChatBotDb** veritabanı, AI chatbot sisteminin tüm verilerini (konuşma geçmişi, bilgi bankası, ürünler, kurallar) saklamak ve yönetmek için tasarlanmıştır.

### 📊 İstatistikler
```sql
-- Tablo sayısı: 7
-- Stored Procedure sayısı: 9
-- Foreign Key sayısı: 1
-- İndeks sayısı: 2 (1 PK otomatik + 1 manuel)
```

### 🗂️ Tablo Listesi

| Tablo Adı | Amaç | Kayıt Tipi |
|-----------|------|-----------|
| **ChatSessions** | Konuşma oturumları | Transactional |
| **ChatMessages** | Mesaj geçmişi | Transactional |
| **KnowledgeBase** | Bilgi bankası (RAG) | Master Data |
| **Products** | Ürün kataloğu | Master Data |
| **PaymentMethods** | Ödeme seçenekleri | Reference Data |
| **ReturnPolicies** | İade politikaları | Reference Data |
| **ShippingRules** | Kargo kuralları | Reference Data |

---

## 2. Tablo Yapıları

### 2.1 ChatSessions

**Amaç:** Kullanıcıların konuşma oturumlarını takip eder. 

#### Tablo Tanımı
```sql
CREATE TABLE [dbo].[ChatSessions](
    [SessionId] NVARCHAR(100) NOT NULL PRIMARY KEY,
    [UserId] NVARCHAR(100) NULL,
    [UserName] NVARCHAR(200) NULL,
    [StartDate] DATETIME NOT NULL DEFAULT GETDATE(),
    [LastActivityDate] DATETIME NOT NULL DEFAULT GETDATE(),
    [MessageCount] INT NOT NULL DEFAULT 0,
    [IsActive] BIT NOT NULL DEFAULT 1
)
```

#### Kolonlar

| Kolon | Tip | Açıklama | Örnek |
|-------|-----|----------|-------|
| **SessionId** | NVARCHAR(100) | Benzersiz oturum ID (PK) | `"user123-session-001"` |
| **UserId** | NVARCHAR(100) | Kullanıcı ID (NULL olabilir) | `"user123"` |
| **UserName** | NVARCHAR(200) | Kullanıcı adı | `"Ahmet Yılmaz"` |
| **StartDate** | DATETIME | Oturum başlangıç | `2025-11-26 14:30:00` |
| **LastActivityDate** | DATETIME | Son aktivite zamanı | `2025-11-26 14:35:00` |
| **MessageCount** | INT | Toplam mesaj sayısı | `5` |
| **IsActive** | BIT | Aktif mi? | `1` (True) |

#### Örnek Veri
```sql
INSERT INTO ChatSessions (SessionId, UserId, UserName)
VALUES ('test-session-001', 'user1', 'Ziyaretçi');
```

#### Kullanım Senaryoları
1. **Yeni session oluştur:** `sp_UpsertChatSession` çağrılır. 
2. **Mesaj eklendiğinde:** `LastActivityDate` ve `MessageCount` güncellenir.
3. **Session sil:** `sp_ClearChatSession` ile tüm mesajlar ve session silinir.

---

### 2.2 ChatMessages

**Amaç:** Konuşma geçmişindeki her mesajı saklar.

#### Tablo Tanımı
```sql
CREATE TABLE [dbo].[ChatMessages](
    [MessageId] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [SessionId] NVARCHAR(100) NOT NULL,
    [Role] NVARCHAR(20) NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
    FOREIGN KEY ([SessionId]) REFERENCES [ChatSessions]([SessionId])
)
```

#### Kolonlar

| Kolon | Tip | Açıklama | Örnek |
|-------|-----|----------|-------|
| **MessageId** | BIGINT | Auto-increment ID (PK) | `1, 2, 3... ` |
| **SessionId** | NVARCHAR(100) | Oturum ID (FK) | `"test-session-001"` |
| **Role** | NVARCHAR(20) | Mesajın sahibi | `"user"`, `"assistant"`, `"system"` |
| **Content** | NVARCHAR(MAX) | Mesaj içeriği | `"Ürün fiyatları nedir?"` |
| **CreatedDate** | DATETIME | Oluşturulma zamanı | `2025-11-26 14:30:15` |

#### Rol Tipleri
```
"user"       → Kullanıcının mesajı
"assistant"  → AI'nın cevabı
"system"     → System prompt (genellikle kaydedilmez)
```

#### Örnek Veri
```sql
INSERT INTO ChatMessages (SessionId, Role, Content)
VALUES 
    ('test-session-001', 'user', 'Ürün fiyatları nedir?'),
    ('test-session-001', 'assistant', 'Ürün A: 500 TL, Ürün B: 1500 TL.. .');
```

#### Foreign Key Kısıtı
```sql
-- SessionId silinirse, o session'a ait tüm mesajlar da silinir (CASCADE)
ALTER TABLE ChatMessages
ADD CONSTRAINT FK_ChatMessages_Sessions
FOREIGN KEY (SessionId) REFERENCES ChatSessions(SessionId)
ON DELETE CASCADE;
```

---

### 2.3 KnowledgeBase

**Amaç:** RAG (Retrieval-Augmented Generation) için bilgi bankası.

#### Tablo Tanımı
```sql
CREATE TABLE [dbo].[KnowledgeBase](
    [DocumentId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Title] NVARCHAR(300) NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL,
    [Category] NVARCHAR(100) NULL,
    [Tags] NVARCHAR(500) NULL,
    [ViewCount] INT NOT NULL DEFAULT 0,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
    [UpdatedDate] DATETIME NULL,
    [Price] DECIMAL(18,2) NULL  -- ✅ Yeni eklendi (ürünler için)
)
```

#### Kolonlar

| Kolon | Tip | Açıklama | Örnek |
|-------|-----|----------|-------|
| **DocumentId** | INT | Auto-increment ID (PK) | `1, 2, 3...` |
| **Title** | NVARCHAR(300) | Belge başlığı | `"Kargo Bilgileri"` |
| **Content** | NVARCHAR(MAX) | Belge içeriği | `"100 TL üzeri kargo ücretsizdir..."` |
| **Category** | NVARCHAR(100) | Kategori | `"Kargo"`, `"Ürün"`, `"FAQ"` |
| **Tags** | NVARCHAR(500) | Arama etiketleri | `"kargo,teslimat,ücret"` |
| **ViewCount** | INT | Görüntülenme sayısı | `15` |
| **IsActive** | BIT | Aktif mi? | `1` (True) |
| **CreatedDate** | DATETIME | Oluşturulma | `2025-11-26 10:00:00` |
| **UpdatedDate** | DATETIME | Son güncelleme | `2025-11-26 15:30:00` |
| **Price** | DECIMAL(18,2) | Fiyat (ürünler için) | `500.00` |

#### Kategori Tipleri
```
"Ürün"       → Ürün bilgileri (Price dolu)
"Kampanya"   → Kampanya detayları
"FAQ"        → Sık sorulan sorular
"Kargo"      → Kargo bilgileri
"İade"       → İade politikası
"Garanti"    → Garanti koşulları
"Bilgi"      → Teknik özellikler, kullanım kılavuzu
```

#### Örnek Veri
```sql
INSERT INTO KnowledgeBase (Title, Content, Category, Tags, Price)
VALUES 
    ('Kargo Bilgileri', '100 TL üzeri ücretsiz kargo... ', 'Kargo', 'kargo,teslimat', NULL),
    ('Ürün A', 'Ürün A: 500. 00 TL... ', 'Ürün', 'ürün a,elektronik', 500.00);
```

#### İndeks (Manuel Oluşturuldu)
```sql
CREATE INDEX IX_KnowledgeBase_Price 
ON KnowledgeBase(Price) 
WHERE IsActive = 1 AND Category = 'Ürün';
```
**Amaç:** Fiyat filtreli sorguları hızlandırır.

---

### 2.4 Products

**Amaç:** Ürün kataloğu (E-Ticaret).  KnowledgeBase'e senkronize edilir.

#### Tablo Tanımı
```sql
CREATE TABLE [dbo].[Products](
    [ProductId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [ProductCode] NVARCHAR(50) NOT NULL UNIQUE,
    [ProductName] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    [Price] DECIMAL(18,2) NOT NULL,
    [StockQuantity] INT NOT NULL DEFAULT 0,
    [Category] NVARCHAR(100) NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
    [UpdatedDate] DATETIME NULL
)
```

#### Kolonlar

| Kolon | Tip | Açıklama | Örnek |
|-------|-----|----------|-------|
| **ProductId** | INT | Auto-increment ID (PK) | `1, 2, 3... ` |
| **ProductCode** | NVARCHAR(50) | Ürün kodu (UNIQUE) | `"PRD-001"` |
| **ProductName** | NVARCHAR(200) | Ürün adı | `"Ürün A"` |
| **Description** | NVARCHAR(MAX) | Açıklama | `"Yüksek kaliteli..."` |
| **Price** | DECIMAL(18,2) | Fiyat | `500.00` |
| **StockQuantity** | INT | Stok adedi | `50` |
| **Category** | NVARCHAR(100) | Kategori | `"Elektronik"` |
| **IsActive** | BIT | Aktif mi?  | `1` |
| **CreatedDate** | DATETIME | Oluşturulma | `2025-11-26 10:00:00` |
| **UpdatedDate** | DATETIME | Güncelleme | `NULL` |

#### UNIQUE Constraint
```sql
-- ProductCode benzersiz olmalı
ALTER TABLE Products 
ADD CONSTRAINT UQ_ProductCode UNIQUE (ProductCode);
```

#### Örnek Veri
```sql
INSERT INTO Products (ProductCode, ProductName, Description, Price, StockQuantity, Category)
VALUES 
    ('PRD-001', 'Ürün A', 'Dayanıklı ve kullanışlı', 500.00, 50, 'Elektronik'),
    ('PRD-002', 'Ürün B', 'Premium kalite', 1500.00, 30, 'Elektronik');
```

---

### 2.5 PaymentMethods

**Amaç:** Ödeme seçeneklerini tanımlar (Reference Data).

#### Tablo Tanımı
```sql
CREATE TABLE [dbo].[PaymentMethods](
    [PaymentMethodId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [MethodName] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [HasInstallment] BIT NOT NULL DEFAULT 0,
    [MaxInstallments] INT NULL,
    [IsActive] BIT NOT NULL DEFAULT 1
)
```

#### Kolonlar

| Kolon | Tip | Açıklama | Örnek |
|-------|-----|----------|-------|
| **PaymentMethodId** | INT | Auto-increment ID | `1, 2, 3...` |
| **MethodName** | NVARCHAR(100) | Ödeme yöntemi adı | `"Kredi Kartı"` |
| **Description** | NVARCHAR(500) | Açıklama | `"Tüm banka kartları kabul edilir"` |
| **HasInstallment** | BIT | Taksit var mı? | `1` (True) |
| **MaxInstallments** | INT | Maksimum taksit | `12` |
| **IsActive** | BIT | Aktif mi? | `1` |

#### Örnek Veri
```sql
INSERT INTO PaymentMethods (MethodName, Description, HasInstallment, MaxInstallments)
VALUES 
    ('Kredi Kartı', 'Tek çekim veya taksit', 1, 12),
    ('Banka Havalesi', 'Havale/EFT ile ödeme', 0, NULL),
    ('Kapıda Ödeme', 'Nakit veya kredi kartı', 0, NULL);
```

---

### 2.6 ReturnPolicies

**Amaç:** İade politikalarını saklar.

#### Tablo Tanımı
```sql
CREATE TABLE [dbo].[ReturnPolicies](
    [PolicyId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [PolicyName] NVARCHAR(200) NOT NULL,
    [ReturnPeriodDays] INT NOT NULL,
    [Conditions] NVARCHAR(MAX) NULL,
    [ReturnShippingCost] DECIMAL(18,2) NULL,
    [IsActive] BIT NOT NULL DEFAULT 1
)
```

#### Örnek Veri
```sql
INSERT INTO ReturnPolicies (PolicyName, ReturnPeriodDays, Conditions, ReturnShippingCost)
VALUES ('Standart İade', 14, 'Ürün kullanılmamış ve ambalajında olmalıdır', 0.00);
```

---

### 2.7 ShippingRules

**Amaç:** Sipariş tutarına göre kargo ücreti hesaplama kuralları.

#### Tablo Tanımı
```sql
CREATE TABLE [dbo].[ShippingRules](
    [RuleId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [MinOrderAmount] DECIMAL(18,2) NOT NULL,
    [MaxOrderAmount] DECIMAL(18,2) NULL,
    [ShippingCost] DECIMAL(18,2) NOT NULL,
    [DeliveryDaysMin] INT NOT NULL,
    [DeliveryDaysMax] INT NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [IsActive] BIT NOT NULL DEFAULT 1
)
```

#### Kolonlar

| Kolon | Tip | Açıklama | Örnek |
|-------|-----|----------|-------|
| **RuleId** | INT | Auto-increment ID | `1, 2, 3...` |
| **MinOrderAmount** | DECIMAL(18,2) | Min sipariş tutarı | `0.00` |
| **MaxOrderAmount** | DECIMAL(18,2) | Max sipariş tutarı (NULL = sınırsız) | `99.99` |
| **ShippingCost** | DECIMAL(18,2) | Kargo ücreti | `30.00` |
| **DeliveryDaysMin** | INT | Min teslimat günü | `2` |
| **DeliveryDaysMax** | INT | Max teslimat günü | `5` |
| **Description** | NVARCHAR(500) | Açıklama | `"100 TL altı siparişler"` |
| **IsActive** | BIT | Aktif mi? | `1` |

#### Örnek Veri
```sql
INSERT INTO ShippingRules (MinOrderAmount, MaxOrderAmount, ShippingCost, DeliveryDaysMin, DeliveryDaysMax, Description)
VALUES 
    (0.00, 99.99, 30.00, 2, 5, '100 TL altı siparişler için kargo ücreti'),
    (100.00, NULL, 0.00, 2, 5, '100 TL ve üzeri ücretsiz kargo');
```

---

## 3. İlişkiler ve Kısıtlamalar

### 3. 1 Foreign Keys

```
ChatMessages.SessionId  →  ChatSessions.SessionId (CASCADE DELETE)
```

**Açıklama:**
- `ChatSessions` silinirse, o session'a ait tüm `ChatMessages` otomatik silinir. 

### 3.2 UNIQUE Constraints

```
Products.ProductCode  →  UNIQUE
```

**Açıklama:**
- Aynı ürün kodu iki kez eklenemez. 

### 3.3 Default Values

| Tablo | Kolon | Default |
|-------|-------|---------|
| ChatSessions | StartDate | GETDATE() |
| ChatSessions | LastActivityDate | GETDATE() |
| ChatSessions | MessageCount | 0 |
| ChatSessions | IsActive | 1 |
| ChatMessages | CreatedDate | GETDATE() |
| KnowledgeBase | ViewCount | 0 |
| KnowledgeBase | IsActive | 1 |
| KnowledgeBase | CreatedDate | GETDATE() |
| Products | StockQuantity | 0 |
| Products | IsActive | 1 |
| Products | CreatedDate | GETDATE() |

---

## 4. Stored Procedures

### 4.1 sp_UpsertChatSession

**Amaç:** Session yoksa oluştur, varsa güncelle (Upsert işlemi).

```sql
CREATE PROCEDURE sp_UpsertChatSession
    @SessionId NVARCHAR(100),
    @UserId NVARCHAR(100),
    @UserName NVARCHAR(200)
AS
BEGIN
    IF EXISTS (SELECT 1 FROM ChatSessions WHERE SessionId = @SessionId)
    BEGIN
        UPDATE ChatSessions
        SET LastActivityDate = GETDATE(),
            MessageCount = MessageCount + 1
        WHERE SessionId = @SessionId;
    END
    ELSE
    BEGIN
        INSERT INTO ChatSessions (SessionId, UserId, UserName)
        VALUES (@SessionId, @UserId, @UserName);
    END
END
```

**Kullanım:**
```sql
EXEC sp_UpsertChatSession 
    @SessionId = 'test-session-001', 
    @UserId = 'user1', 
    @UserName = 'Ziyaretçi';
```

**Senaryo:**
1. İlk mesajda: Session oluşturulur. 
2. Sonraki mesajlarda: `LastActivityDate` güncellenir, `MessageCount` artar. 

---

### 4.2 sp_SaveChatMessage

**Amaç:** Mesajı ChatMessages tablosuna ekler.

```sql
CREATE PROCEDURE sp_SaveChatMessage
    @SessionId NVARCHAR(100),
    @Role NVARCHAR(20),
    @Content NVARCHAR(MAX)
AS
BEGIN
    INSERT INTO ChatMessages (SessionId, Role, Content)
    VALUES (@SessionId, @Role, @Content);
END
```

**Kullanım:**
```sql
EXEC sp_SaveChatMessage 
    @SessionId = 'test-session-001', 
    @Role = 'user', 
    @Content = 'Ürün fiyatları nedir?';
```

---

### 4.3 sp_GetChatHistory

**Amaç:** Session'a ait tüm mesajları getirir (tarih sıralı).

```sql
CREATE PROCEDURE sp_GetChatHistory
    @SessionId NVARCHAR(100)
AS
BEGIN
    SELECT MessageId, Role, Content, CreatedDate
    FROM ChatMessages
    WHERE SessionId = @SessionId
    ORDER BY CreatedDate;
END
```

**Kullanım:**
```sql
EXEC sp_GetChatHistory @SessionId = 'test-session-001';
```

**Dönen Veri:**
```
MessageId | Role      | Content                | CreatedDate
----------|-----------|------------------------|-------------------
1         | user      | Ürün fiyatları nedir?   | 2025-11-26 14:30:00
2         | assistant | Ürün A: 500 TL...       | 2025-11-26 14:30:05
```

---

### 4.4 sp_ClearChatSession

**Amaç:** Session ve tüm mesajları siler (Transaction içinde).

```sql
CREATE PROCEDURE sp_ClearChatSession
    @SessionId NVARCHAR(100)
AS
BEGIN
    BEGIN TRANSACTION;
    
    DELETE FROM ChatMessages WHERE SessionId = @SessionId;
    DELETE FROM ChatSessions WHERE SessionId = @SessionId;
    
    COMMIT TRANSACTION;
END
```

**Kullanım:**
```sql
EXEC sp_ClearChatSession @SessionId = 'test-session-001';
```

**Açıklama:**
- Önce `ChatMessages` silinir (FK yüzünden).
- Sonra `ChatSessions` silinir. 
- Transaction: İki işlem de başarılı olmazsa rollback.

---

### 4.5 sp_SearchKnowledgeBase

**Amaç:** RAG için bilgi bankasında arama yapar.

```sql
CREATE PROCEDURE sp_SearchKnowledgeBase
    @SearchQuery NVARCHAR(200)
AS
BEGIN
    -- 1. Kelimelere ayır
    DECLARE @Keywords TABLE (Keyword NVARCHAR(100));
    INSERT INTO @Keywords
    SELECT TRIM(value) 
    FROM STRING_SPLIT(@SearchQuery, ' ')
    WHERE LEN(TRIM(value)) > 2;
    
    -- 2. Ara
    SELECT DISTINCT DocumentId, Title, Content, Category, Tags
    FROM KnowledgeBase
    WHERE IsActive = 1
        AND (
            EXISTS (SELECT 1 FROM @Keywords k WHERE Title LIKE '%' + k. Keyword + '%')
            OR Title LIKE '%' + @SearchQuery + '%'
        );
    
    -- 3.  ViewCount artır
    UPDATE KnowledgeBase
    SET ViewCount = ViewCount + 1
    WHERE DocumentId IN (SELECT DocumentId FROM ... );
END
```

**Özellikler:**
1. **Keyword Extraction:** `STRING_SPLIT` ile kelimelere ayırır.
2. **Multi-Keyword Search:** Her keyword için ayrı arama.
3. **ViewCount Tracking:** Bulunan belgeler için sayaç artar.

**Kullanım:**
```sql
EXEC sp_SearchKnowledgeBase @SearchQuery = 'kargo ücreti';
```

---

### 4.6 sp_SmartProductSearch

**Amaç:** Fiyat + Kategori filtreli akıllı ürün arama.

```sql
CREATE PROCEDURE sp_SmartProductSearch
    @SearchQuery NVARCHAR(200),
    @MinPrice DECIMAL(18,2) = NULL,
    @MaxPrice DECIMAL(18,2) = NULL,
    @Category NVARCHAR(100) = NULL
AS
BEGIN
    SELECT Title, Content, Category, Tags
    FROM KnowledgeBase
    WHERE IsActive = 1
        AND Category = 'Ürün'
        AND (Title LIKE '%' + @SearchQuery + '%')
        AND (@MinPrice IS NULL OR Price >= @MinPrice)
        AND (@MaxPrice IS NULL OR Price <= @MaxPrice)
        AND (@Category IS NULL OR Content LIKE '%Kategori: ' + @Category + '%');
END
```

**Kullanım:**
```sql
-- 500-1000 TL arası Bilgisayar ürünleri
EXEC sp_SmartProductSearch 
    @SearchQuery = 'ürün',
    @MinPrice = 500,
    @MaxPrice = 1000,
    @Category = 'Bilgisayar';
```

**Sonuç:**
```
Title                 | Content                         | Category
----------------------|---------------------------------|----------
Ürün H - Webcam 1080p | Ürün H - Webcam 1080p: 650 TL... | Ürün
Ürün I - Mekanik Klavye| Ürün I: 950 TL...              | Ürün
```

---

### 4.7 sp_CalculateShipping

**Amaç:** Sipariş tutarına göre kargo ücretini hesaplar.

```sql
CREATE PROCEDURE sp_CalculateShipping
    @OrderAmount DECIMAL(18,2)
AS
BEGIN
    SELECT TOP 1 ShippingCost, DeliveryDaysMin, DeliveryDaysMax, Description
    FROM ShippingRules
    WHERE IsActive = 1
        AND MinOrderAmount <= @OrderAmount
        AND (MaxOrderAmount IS NULL OR MaxOrderAmount >= @OrderAmount)
    ORDER BY MinOrderAmount DESC;
END
```

**Kullanım:**
```sql
EXEC sp_CalculateShipping @OrderAmount = 150.00;
```

**Sonuç:**
```
ShippingCost | DeliveryDaysMin | DeliveryDaysMax | Description
-------------|-----------------|-----------------|---------------------------
0.00         | 2               | 5               | 100 TL ve üzeri ücretsiz
```

---

### 4.8 sp_GetPaymentMethods

**Amaç:** Aktif ödeme yöntemlerini getirir. 

```sql
CREATE PROCEDURE sp_GetPaymentMethods
AS
BEGIN
    SELECT MethodName, Description, HasInstallment, MaxInstallments
    FROM PaymentMethods
    WHERE IsActive = 1
    ORDER BY MethodName;
END
```

---

### 4.9 sp_GetReturnPolicy

**Amaç:** Aktif iade politikasını getirir.

```sql
CREATE PROCEDURE sp_GetReturnPolicy
AS
BEGIN
    SELECT PolicyName, ReturnPeriodDays, Conditions, ReturnShippingCost
    FROM ReturnPolicies
    WHERE IsActive = 1;
END
```

---

## 5. İndeksler ve Performans

### 5.1 Otomatik İndeksler (Primary Keys)

```sql
-- Tüm PK'ler otomatik CLUSTERED INDEX oluşturur
ChatSessions.SessionId        → CLUSTERED INDEX
ChatMessages.MessageId        → CLUSTERED INDEX
KnowledgeBase.DocumentId      → CLUSTERED INDEX
Products.ProductId            → CLUSTERED INDEX
PaymentMethods.PaymentMethodId→ CLUSTERED INDEX
ReturnPolicies.PolicyId       → CLUSTERED INDEX
ShippingRules.RuleId          → CLUSTERED INDEX
```

### 5.2 Manuel İndeksler

#### IX_KnowledgeBase_Price
```sql
CREATE INDEX IX_KnowledgeBase_Price 
ON KnowledgeBase(Price) 
WHERE IsActive = 1 AND Category = 'Ürün';
```

**Amaç:** Fiyat filtreli sorguları hızlandırır. 

**Kullanıldığı Sorgu:**
```sql
SELECT * FROM KnowledgeBase
WHERE IsActive = 1 
  AND Category = 'Ürün'
  AND Price BETWEEN 500 AND 1000;
```

---

### 5.3 Performans Önerileri

#### 1.  Foreign Key için Index
```sql
-- ChatMessages. SessionId üzerinde index (JOIN hızlandırma)
CREATE INDEX IX_ChatMessages_SessionId 
ON ChatMessages(SessionId);
```

#### 2. Search için Full-Text Index (Gelişmiş)
```sql
-- KnowledgeBase.Content üzerinde full-text search
CREATE FULLTEXT INDEX ON KnowledgeBase(Content)
KEY INDEX PK_KnowledgeBase;
```

#### 3.  Arşivleme için Partitioning
```sql
-- ChatMessages tablosunu tarihe göre partition
-- (6 aydan eski mesajlar ayrı partition'a taşınır)
```

---

## 6.  Veri Akışı Senaryoları

### Senaryo 1: Yeni Chat Konuşması

```
1. USER → POST /api/Chat/message
   Body: { "sessionId": "new-session", "message": "Merhaba" }

2. API → sp_UpsertChatSession
   INSERT INTO ChatSessions (SessionId='new-session', UserId='user1', UserName='Ziyaretçi')

3.  API → sp_SaveChatMessage
   INSERT INTO ChatMessages (SessionId='new-session', Role='user', Content='Merhaba')

4. API → RAG Search (sp_SearchKnowledgeBase)
   SELECT * FROM KnowledgeBase WHERE ...  → 0 sonuç (greeting için bilgi yok)

5. API → LLM Call → Cevap: "Merhaba!  Nasıl yardımcı olabilirim?"

6. API → sp_SaveChatMessage
   INSERT INTO ChatMessages (SessionId='new-session', Role='assistant', Content='Merhaba! .. .')

7. API → sp_UpsertChatSession
   UPDATE ChatSessions SET MessageCount=2, LastActivityDate=GETDATE() WHERE SessionId='new-session'
```

**Veritabanı Durumu:**
```sql
-- ChatSessions
SessionId    | UserId | MessageCount | LastActivityDate
-------------|--------|--------------|-------------------
new-session  | user1  | 2            | 2025-11-26 14:30:05

-- ChatMessages
MessageId | SessionId   | Role      | Content
----------|-------------|-----------|---------------------------
1         | new-session | user      | Merhaba
2         | new-session | assistant | Merhaba! Nasıl yardımcı... 
```

---

### Senaryo 2: RAG ile Ürün Sorgusu

```
1. USER → "Ürün fiyatları nedir?"

2. API → sp_SearchKnowledgeBase('ürün fiyatları')
   Keyword Extraction: ["ürün", "fiyatları"]
   
   Search Results:
   - DocumentId: 1, Title: "Ürün Bilgileri" → ViewCount: 15 → 16
   - DocumentId: 19, Title: "Ürün A" → ViewCount: 1 → 2

3. API → Format RAG Context
   "BİLGİ BANKASI:
    • Ürün Bilgileri: Ürün A: 500 TL, Ürün B: 1500 TL... 
    • Ürün A: Ürün A: 500. 00 TL.  Yüksek kaliteli..."

4. API → LLM (with RAG context)
   System Prompt: "Sen müşteri destek asistanısın.  SADECE bilgi bankasındaki bilgileri kullan..."
   Response: "Ürün A: 500 TL, Ürün B: 1500 TL..."

5. API → Save Messages (sp_SaveChatMessage x2)
```

**Veritabanı Değişimi:**
```sql
-- KnowledgeBase. ViewCount artışı
UPDATE KnowledgeBase SET ViewCount = ViewCount + 1 
WHERE DocumentId IN (1, 19);
```

---

### Senaryo 3: Akıllı Ürün Arama

```
1. USER → POST /api/Chat/smart-search
   Body: { "query": "ürün", "minPrice": 500, "maxPrice": 1000, "category": "Bilgisayar" }

2. API → sp_SmartProductSearch
   WHERE Price BETWEEN 500 AND 1000 
     AND Content LIKE '%Kategori: Bilgisayar%'

3. SQL → Result Set
   - Ürün H - Webcam 1080p (650 TL)
   - Ürün I - Mekanik Klavye (950 TL)
   - Ürün J - Gaming Mouse (450 TL) → EXCLUDED (450 < 500)

4. API → Response
   { "resultCount": 2, "products": [... ] }
```

---

## 7. Bakım ve Optimizasyon

### 7. 1 Veri Temizliği

#### Eski Session'ları Sil (30 günden eski)
```sql
DELETE FROM ChatMessages
WHERE SessionId IN (
    SELECT SessionId FROM ChatSessions
    WHERE LastActivityDate < DATEADD(DAY, -30, GETDATE())
);

DELETE FROM ChatSessions
WHERE LastActivityDate < DATEADD(DAY, -30, GETDATE());
```

#### Pasif Belgeleri Arşivle
```sql
-- IsActive=0 belgeleri arşiv tablosuna taşı
INSERT INTO KnowledgeBase_Archive
SELECT * FROM KnowledgeBase WHERE IsActive = 0;

DELETE FROM KnowledgeBase WHERE IsActive = 0;
```

---

### 7.2 İstatistikler

#### En Çok Aranan Belgeler
```sql
SELECT TOP 10 Title, Category, ViewCount
FROM KnowledgeBase
WHERE IsActive = 1
ORDER BY ViewCount DESC;
```

#### Ortalama Mesaj Sayısı
```sql
SELECT AVG(CAST(MessageCount AS FLOAT)) AS AvgMessagesPerSession
FROM ChatSessions;
```

#### Günlük Session Sayısı
```sql
SELECT CAST(StartDate AS DATE) AS Date, COUNT(*) AS SessionCount
FROM ChatSessions
GROUP BY CAST(StartDate AS DATE)
ORDER BY Date DESC;
```

---

### 7.3 Backup Stratejisi

```sql
-- Full Backup (Haftalık)
BACKUP DATABASE AIChatBotDb 
TO DISK = 'C:\Backups\AIChatBotDb_Full. bak'
WITH FORMAT;

-- Differential Backup (Günlük)
BACKUP DATABASE AIChatBotDb 
TO DISK = 'C:\Backups\AIChatBotDb_Diff.bak'
WITH DIFFERENTIAL;

-- Transaction Log Backup (Saatlik)
BACKUP LOG AIChatBotDb 
TO DISK = 'C:\Backups\AIChatBotDb_Log.trn';
```

---

## 📊 Özet Tablo

| Bileşen | Sayı | Detay |
|---------|------|-------|
| **Tablolar** | 7 | ChatSessions, ChatMessages, KnowledgeBase, Products, PaymentMethods, ReturnPolicies, ShippingRules |
| **Stored Procedures** | 9 | Chat, RAG, E-Ticaret işlemleri |
| **Foreign Keys** | 1 | ChatMessages → ChatSessions |
| **Indexes** | 2 | PK (otomatik) + Price (manuel) |
| **Default Constraints** | 14 | GETDATE(), 0, 1 değerleri |

---

## 🎓 Sonuç

Bu veritabanı şu özelliklere sahiptir:

1. ✅ **Normalize Edilmiş Tasarım**: Veri tekrarı minimumda. 
2. ✅ **Performance Optimized**: Index'ler, Stored Procedures. 
3. ✅ **Referential Integrity**: Foreign Key ile veri tutarlılığı.
4.  ✅ **Scalable**: Partition'lama ve arşivleme yapılabilir.
5. ✅ **Analytics Ready**: ViewCount, MessageCount tracking. 
6. ✅ **Transaction Safe**: BEGIN TRANSACTION kullanımı.

**API + Veritabanı birlikte tam bir enterprise sistem oluşturuyor!  ** 🚀