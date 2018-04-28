-------------------------------------------------------------------------
-- <copyright file="Tables.sql">
-- Copyright © Ladislau Molnar. All rights reserved.
-- </copyright>
-------------------------------------------------------------------------

-- START SECTION

-- ==========================================================================
-- Note about all hashes:
-- The hashes are stored in reverse order from what is the normal result 
-- of hashing. This is done to be consistent with sites like blockchain.info 
-- and blockexporer that display hashes in 'big endian' format.
-- ==========================================================================

-- ==========================================================================
-- TABLE: BlockchainFile
-- Contains information about the blockchain files that were processed.
-- ==========================================================================
CREATE TABLE BlockchainFile (
    BlockchainFileId                     INT PRIMARY KEY            NOT NULL,
    BlockchainFileName                   NVARCHAR (300)             NOT NULL
);

ALTER TABLE BlockchainFile ADD CONSTRAINT BlockchainFile_BlockchainFileName UNIQUE([BlockchainFileName])


-- ==========================================================================
-- TABLE: Block
-- Contains information about the Bitcoin blocks.
-- ==========================================================================
CREATE TABLE Block (
    BlockId                         BIGINT PRIMARY KEY              NOT NULL,
    BlockchainFileId                INT                             NOT NULL,
    BlockVersion                    INT                             NOT NULL,

    -- Note: hash is in reverse order.
    BlockHash                       VARBINARY (32)                  NOT NULL,
    
    -- Note: hash is in reverse order.
    PreviousBlockHash               VARBINARY (32)                  NOT NULL,

	TransactionsCount				INT								NOT NULL,

	--Note: hash is in reverse order
	MerkleRootHash					VARBINARY (32)					NOT NULL,

    BlockTimestamp                  DATETIME                        NOT NULL
);

CREATE INDEX IX_Block_BlockHash ON Block(BlockHash)

-- ==========================================================================
-- TABLE: BitcoinTransaction
-- Contains information about the Bitcoin transactions.
-- ==========================================================================
CREATE TABLE BitcoinTransaction (
    BitcoinTransactionId            BIGINT PRIMARY KEY              NOT NULL,
    BlockId                         BIGINT                          NOT NULL,

    -- Note: hash is in reverse order.
    TransactionHash                 VARBINARY (32)                  NOT NULL,

	TransactionInputsCount			INT								NOT NULL,
	TransactionOutputsCount			INT								NOT NULL,
    TransactionVersion              INT                             NOT NULL,
    TransactionLockTime             INT                             NOT NULL,
	IsSegWit						BIT								NOT NULL
);

CREATE INDEX IX_BitcoinTransaction_TransactionHash ON BitcoinTransaction(TransactionHash)
CREATE INDEX IX_BitcoinTransaction_BlockId ON BitcoinTransaction(BlockId)


-- ==========================================================================
-- TABLE: TransactionInput
-- Contains information about the Bitcoin transaction inputs.
-- ==========================================================================
CREATE TABLE TransactionInput (
    TransactionInputId              BIGINT PRIMARY KEY              NOT NULL,
    BitcoinTransactionId            BIGINT                          NOT NULL,

    -- The hash of the transaction that contains the output that is the source
    -- of this input.
    -- Note: hash is in reverse order.
	SourceTransactionHash           VARBINARY (32)                  NOT NULL,
    
    -- The index of the output that will be consumed by this input.
    -- The index is a zero based index in the list of outputs of the 
    -- transaction that it belongs to.
    -- Set to -1 to indicate that this input refers to no previous output.
    SourceTransactionOutputIndex    INT								NULL,

    SourceTransactionId       BIGINT                          NULL,
	ScriptSig				  VARBINARY (MAX)			      NULL,
	WitnessStack			  VARBINARY (MAX)				  NULL
);

CREATE INDEX IX_TransactionInput_BitcoinTransactionId ON TransactionInput(BitcoinTransactionId)
CREATE INDEX IX_TransactionInput_SourceTransactionId ON TransactionInput(SourceTransactionId)
CREATE INDEX IX_TransactionInput_SourceTransactionHash ON TransactionInput(SourceTransactionHash)


-- ==========================================================================
-- TABLE: TransactionOutput
-- Contains information about the Bitcoin transaction outputs.
-- ==========================================================================
CREATE TABLE TransactionOutput (
    TransactionOutputId             BIGINT PRIMARY KEY              NOT NULL,
    BitcoinTransactionId            BIGINT                          NOT NULL,
    OutputIndex                     INT                             NOT NULL,
    OutputValueBtc                  NUMERIC(20,8)                   NOT NULL,
    OutputScript                    VARBINARY (MAX)                 NOT NULL,
	OutputAddress					NVARCHAR(300)					NOT NULL,
	OutputAddressId					BIGINT							NULL
);

CREATE INDEX IX_TransactionOutput_BitcoinTransactionId ON TransactionOutput(BitcoinTransactionId)
CREATE INDEX IX_TransactionOutput_OutputAddress ON TransactionOutput(OutputAddress)
CREATE INDEX IX_TransactionOutput_OutputAddressId ON TransactionOutput(OutputAddressId)

-- ==========================================================================
-- TABLE: BtcDbSettings
-- System reserved. Key-value pairs containing system data.
-- ==========================================================================
CREATE TABLE BtcDbSettings (
    PropertyName                    NVARCHAR (32)                   NOT NULL,
    PropertyValue                   NVARCHAR (MAX)                  NOT NULL
)

INSERT INTO BtcDbSettings(PropertyName, PropertyValue) VALUES('DB-VERSION', 1)