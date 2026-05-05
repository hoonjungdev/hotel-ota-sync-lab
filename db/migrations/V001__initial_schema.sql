-- V001: Initial schema for hotel OTA sync lab
--
-- Five tables: property, room_type, rate_plan, inventory, reservation.
-- Money is stored in ISO 4217 minor units (BIGINT) to avoid float drift.
-- Idempotency on reservation ingestion is enforced by
-- UNIQUE (channel, channel_booking_id).

CREATE TABLE property (
    id          BIGSERIAL PRIMARY KEY,
    code        TEXT        NOT NULL UNIQUE,
    name        TEXT        NOT NULL,
    timezone    TEXT        NOT NULL,           -- IANA tz, e.g. 'Asia/Seoul'
    currency    CHAR(3)     NOT NULL,           -- ISO 4217
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE room_type (
    id             BIGSERIAL PRIMARY KEY,
    property_id    BIGINT      NOT NULL REFERENCES property(id),
    code           TEXT        NOT NULL,
    name           TEXT        NOT NULL,
    max_occupancy  SMALLINT    NOT NULL CHECK (max_occupancy > 0),
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (property_id, code)
);

CREATE TABLE rate_plan (
    id            BIGSERIAL PRIMARY KEY,
    property_id   BIGINT      NOT NULL REFERENCES property(id),
    room_type_id  BIGINT      NOT NULL REFERENCES room_type(id),
    code          TEXT        NOT NULL,
    name          TEXT        NOT NULL,
    refundable    BOOLEAN     NOT NULL,
    board_basis   TEXT        NOT NULL,         -- 'RO', 'BB', 'HB', 'FB', 'AI'
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (property_id, code)
);

-- ARI snapshot per (property, room_type, rate_plan, stay_date).
-- v1 keeps availability and price in one row; a v2 PR will split rate out.
CREATE TABLE inventory (
    property_id      BIGINT      NOT NULL REFERENCES property(id),
    room_type_id     BIGINT      NOT NULL REFERENCES room_type(id),
    rate_plan_id     BIGINT      NOT NULL REFERENCES rate_plan(id),
    stay_date        DATE        NOT NULL,
    available_count  INT         NOT NULL CHECK (available_count >= 0),
    amount_minor     BIGINT      NOT NULL CHECK (amount_minor >= 0),
    currency         CHAR(3)     NOT NULL,
    version          BIGINT      NOT NULL DEFAULT 0,  -- optimistic concurrency
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (property_id, room_type_id, rate_plan_id, stay_date)
);

CREATE INDEX idx_inventory_stay_date ON inventory(stay_date);

CREATE TABLE reservation (
    id                  BIGSERIAL PRIMARY KEY,
    channel             TEXT        NOT NULL,   -- 'BlueWave', 'SkyTrip', ...
    channel_booking_id  TEXT        NOT NULL,
    property_id         BIGINT      NOT NULL REFERENCES property(id),
    room_type_id        BIGINT      NOT NULL REFERENCES room_type(id),
    rate_plan_id        BIGINT      NOT NULL REFERENCES rate_plan(id),
    check_in            DATE        NOT NULL,
    check_out           DATE        NOT NULL CHECK (check_out > check_in),
    guests_count        SMALLINT    NOT NULL CHECK (guests_count > 0),
    status              TEXT        NOT NULL,   -- 'Confirmed', 'Cancelled', ...
    total_amount_minor  BIGINT      NOT NULL CHECK (total_amount_minor >= 0),
    currency            CHAR(3)     NOT NULL,
    received_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    raw_payload         JSONB       NOT NULL,
    UNIQUE (channel, channel_booking_id)        -- idempotent ingestion key
);

CREATE INDEX idx_reservation_property_dates
    ON reservation (property_id, check_in, check_out);
