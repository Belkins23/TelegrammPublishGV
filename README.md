# TelegrammPublishGV

Web API для публикации заказов в Telegram-канал с интерактивными кнопками: курьер может взять заказ, отметить доставку или отказаться. Состояния заказов дублируются в RabbitMQ.

## Возможности

- **Публикация заказа** — POST-запрос с текстом (HTML), кнопкой «Забрать заказ» и при необходимости своими кнопками.
- **Обработка нажатий** — при нажатии «Забрать заказ» показывается, кто забирает, и появляются кнопки «Доставил» и «Отказаться». Нажать «Доставил» или «Отказаться» может только тот, кто взял заказ.
- **Отказ от заказа** — сообщение «Заказ заберёт» удаляется; если в Redis сохранён payload заказа, он переопубликовывается с кнопкой «Забрать заказ».
- **Удаление публикации** — удаление сообщения заказа в канале по id заказа (DELETE).
- **Получение обновлений** — через long polling (getUpdates) или webhook.
- **Redis** — хранение payload заказа и расположения сообщения (для переопубликации при отказе и удаления по id).
- **RabbitMQ** — публикация событий состояний заказа (Published, Taken, Delivered, Refused, Deleted) в очередь для внешних систем.

## Стек

- .NET 10, ASP.NET Core
- Serilog, Swagger (OpenAPI)
- Newtonsoft.Json
- Redis (StackExchange.Redis)
- RabbitMQ (RabbitMQ.Client)
- Telegram Bot API

## Требования

- .NET 10 SDK
- Доступ к Redis и RabbitMQ (настройки в `appsettings.json`)
- Telegram-бот с токеном и канал, куда бот может публиковать сообщения

## Конфигурация (appsettings.json)

| Секция | Описание |
|--------|----------|
| **Telegram** | `BotToken`, `ChannelId`; при webhook — `UseWebhook`, `WebhookBaseUrl` |
| **RedisSettings** | `ConnectionString` для Redis |
| **RabbitMQ_States** | `Enable`, `HostName`, `Port`, `UserName`, `Password`, `QueueName` — очередь для событий заказов |

По умолчанию приложение слушает **http://0.0.0.0:5689**.

## API

- **POST /api/publish** — публикация сообщения в канал. Тело — JSON в формате Telegram sendMessage: `chat_id` (необязательно), `text`, `parse_mode` (например `"HTML"`), `reply_markup` с `inline_keyboard`.
- **DELETE /api/publish/order/{orderId}** — удаление публикации заказа из канала по id заказа.
- **POST /api/telegram/webhook** — приём обновлений от Telegram (используется при `UseWebhook: true`).

Документация и проверка запросов: **http://localhost:5689/swagger**.

## Запуск

```bash
dotnet restore
dotnet run
```

Приложение будет доступно по адресу http://localhost:5689 (или http://0.0.0.0:5689 с других машин).

## Режимы получения обновлений от Telegram

- **getUpdates (по умолчанию)** — `UseWebhook: false` или не задан. Фоновый сервис опрашивает Telegram; не нужен публичный URL.
- **Webhook** — `UseWebhook: true` и `WebhookBaseUrl: "https://ваш-домен"`. При старте регистрируется URL `https://ваш-домен/api/telegram/webhook`. Нужен HTTPS и доступный из интернета адрес.

## RabbitMQ: форматы сообщений в очереди

В очередь `order_delivery_states` (или имя из `QueueName`) отправляется JSON с полями: `orderId`, `status`, `timestamp`; при необходимости — `messageId`, `chatId`, `userId`, `userName`, `republished`.

- **Published** — заказ опубликован (`orderId`, `messageId`, `chatId`).
- **Taken** — заказ взят (`orderId`, `userId`, `userName`).
- **Delivered** — доставлен (`orderId`, `userId`, `userName`).
- **Refused** — отказ от заказа (`orderId`, `userId`, `userName`, `republished`).
- **Deleted** — публикация удалена по API (`orderId`).

## Лицензия

Проект для внутреннего использования.
