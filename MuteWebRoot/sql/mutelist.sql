-- phpMyAdmin SQL Dump
-- version 4.5.4.1deb2ubuntu2
-- http://www.phpmyadmin.net
--
-- Host: localhost
-- Generation Time: Dec 04, 2017 at 11:54 AM
-- Server version: 5.7.20
-- PHP Version: 7.0.22-0ubuntu0.16.04.1

--
-- Database: `osmodules`
--

-- --------------------------------------------------------

--
-- Table structure for table `mutelist`
--

CREATE TABLE IF NOT EXISTS `mutelist` (
  `AgentID` char(36) COLLATE utf8_unicode_ci NOT NULL,
  `MuteID` char(36) COLLATE utf8_unicode_ci NOT NULL,
  `MuteName` varchar(255) COLLATE utf8_unicode_ci NOT NULL,
  `type` int(11) UNSIGNED NOT NULL,
  `flags` int(11) UNSIGNED NOT NULL,
  `Stamp` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY `AgentID_2` (`AgentID`,`MuteID`) USING BTREE,
  KEY `AgentID` (`AgentID`) USING BTREE
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;
