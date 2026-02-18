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

## Мониторинг (Prometheus + Grafana)

Приложение отдаёт метрики в формате Prometheus по адресу **GET http://localhost:5689/metrics**. По ним можно понять, жив ли сервис, и строить графики в Grafana.

### 1. Проверка, что метрики доступны

В браузере или через curl:

```bash
curl http://localhost:5689/metrics
```

Должен вернуться текст с метриками (строки вида `# TYPE ...`, `metric_name value`). Если ответ есть — сервис жив и Prometheus сможет его скрапить.

### 2. Настройка Prometheus

Добавь в конфиг Prometheus (`prometheus.yml`) цель для нашего сервиса:

```yaml
scrape_configs:
  - job_name: 'telegramm-publish-gv'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:5689']
```

Если сервис на другом хосте (или в Docker), укажи его адрес вместо `localhost`, например `host.docker.internal:5689` или `192.168.1.10:5689`.

Перезапусти Prometheus и убедись, что в **Status → Targets** цель `telegramm-publish-gv` в состоянии **UP**.

### 3. Grafana: источник данных

1. В Grafana: **Configuration (шестерёнка) → Data sources → Add data source**.
2. Выбери **Prometheus**.
3. Укажи URL до Prometheus (например `http://localhost:9090` или `http://prometheus:9090` в Docker).
4. Сохрани (**Save & test**).

### 4. Grafana: панель «Сервис жив / не жив»

**Вариант A — одна панель «жив/не жив» (Stat или Gauge):**

1. Создай дашборд или открой существующий → **Add panel**.
2. В запросе выбери **Prometheus** и в поле запроса введи:

   ```promql
   up{job="telegramm-publish-gv"}
   ```

   Метрика `up` есть у Prometheus по умолчанию: **1** — скрап успешен (сервис жив), **0** — скрап не удался (сервис не отвечает или недоступен).

3. В настройках панели:
   - **Visualization**: выбери **Stat** или **Gauge**.
   - В **Field** (или **Standard options**) задай **Unit** → **none** и при желании **Min** = 0, **Max** = 1.
   - В **Thresholds** можно задать: зелёный при значении 1, красный при 0 (например: Base = 0 — красный, добавить threshold 1 — зелёный).
   - **Title** панели, например: `TelegrammPublishGV — статус`.

Так на дашборде будет одно число: **1** = жив, **0** = не жив.

**Вариант B — текст «Жив» / «Не жив»:**

1. Та же панель с запросом `up{job="telegramm-publish-gv"}`.
2. **Visualization** → **Stat**.
3. В **Value options** включи **Color mode** → **Background** и настрой **Thresholds**: 0 — красный, 1 — зелёный.
4. В **Text mode** (если есть) или через **Overrides** можно вывести для значения 1 текст «Жив», для 0 — «Не жив» (в части плагинов это делается через **Mappings** или кастомные переопределения полей).

**Вариант C — график по времени:**

1. Запрос тот же: `up{job="telegramm-publish-gv"}`.
2. **Visualization** → **Time series** (график).
3. На графике будет линия 0 или 1: видно, когда сервис был жив (1) и когда падал (0).

### Кратко

| Что сделать | Где |
|-------------|-----|
| Метрики сервиса | GET http://localhost:5689/metrics |
| Добавить цель в Prometheus | `prometheus.yml` → job `telegramm-publish-gv`, target `:5689` |
| В Grafana увидеть «жив/не жив» | Панель с запросом `up{job="telegramm-publish-gv"}`, визуализация Stat/Gauge или Time series |

В репозитории лежит готовый дашборд **grafana-dashboard-service-health.json**: в нём есть панель **TelegrammPublishGV** (Stat по `up{job="telegramm-publish-gv"}`) и таблица **TelegrammPublishGV — Backends (Redis, RabbitMQ)** по метрике `telegramm_backend_info` (хост и порт Redis и RabbitMQ без паролей). Импорт в Grafana: **Dashboard → Import → Upload JSON file**; при необходимости укажи свой Data source (uid Prometheus в JSON: `efb0pky7v6scge`).

## Лицензия

Проект для внутреннего использования.
