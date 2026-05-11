CREATE DATABASE IF NOT EXISTS inventory;
USE inventory;

CREATE TABLE IF NOT EXISTS customers (
  id INT NOT NULL AUTO_INCREMENT,
  first_name VARCHAR(255) NOT NULL,
  last_name VARCHAR(255) NOT NULL,
  email VARCHAR(255) NOT NULL,
  PRIMARY KEY (id)
);

INSERT INTO customers(first_name, last_name, email)
VALUES
('Anne', 'Kretchmar', 'annek@noanswer.org'),
('Maggie', 'Smith', 'maggie@example.com');
