﻿using System;
                    MessageBox.Show($"A file with the key {Key} already exists. Please choose a different key.", @"Key Exists", MessageBoxButtons.OK,
                      MessageBoxIcon.Error);
                    DialogResult = DialogResult.OK;
                    Close();
                }