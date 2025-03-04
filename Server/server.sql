-- Tabelle für Benutzer
CREATE TABLE users
(
    id            SERIAL PRIMARY KEY,
    username      VARCHAR(50)  NOT NULL UNIQUE,
    password_hash VARCHAR(200) NOT NULL,
    role          VARCHAR(50)  NOT NULL
);

-- Tabelle für Gruppen (Kanäle)
CREATE TABLE groups
(
    id   SERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL UNIQUE
);

-- Tabelle, die festhält, welche Benutzer in welcher Gruppe sind
CREATE TABLE group_membership
(
    group_id INT NOT NULL REFERENCES groups (id),
    user_id  INT NOT NULL REFERENCES users (id),
    PRIMARY KEY (group_id, user_id)
);

-- Tabelle für Nachrichten
CREATE TABLE messages
(
    id          SERIAL PRIMARY KEY,
    sender_id   INT       NOT NULL REFERENCES users (id),
    receiver_id INT, -- bei privatem Chat
    group_id    INT, -- bei Gruppenchats
    content     TEXT      NOT NULL,
    timestamp   TIMESTAMP NOT NULL DEFAULT NOW()
);