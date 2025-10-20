import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators, FormsModule } from '@angular/forms';
import { TuiButton, TuiTextfield, TuiHint } from '@taiga-ui/core';
import { TuiExpand } from '@taiga-ui/core/components/expand';
import { TranslocoService, TranslocoModule } from '@jsverse/transloco';
import { LocaleAwareDatePipe } from '../../../../utils/locale-aware-date.pipe';
// TuiSwitch removed: using native checkboxes for these boolean controls
import { firstValueFrom } from 'rxjs';
import { EmailService } from '../../../../client/services/email.service';
import { EmailProviderType } from '../../../../client/models/email-provider-type';
import { SaveEmailSettingsRequest } from '../../../../client/models/save-email-settings-request';
import { EmailSettingsDto } from '../../../../client/models/email-settings-dto';
import { ToastService } from '../../../../services/toast.service';
import { SaveEmailCriteriaRequest } from '../../../../client/models/save-email-criteria-request';
import { EmailCriteriaDto } from '../../../../client/models/email-criteria-dto';
import { EmailSortBy } from '../../../../client/models/email-sort-by';
import { Int32ApiResponse } from '../../../../client/models/int-32-api-response';

type ConnectionFormControls = {
  providerType: FormControl<EmailProviderType>;
  server: FormControl<string>;
  port: FormControl<number>;
  useSsl: FormControl<boolean>;
  username: FormControl<string>;
  password: FormControl<string>;
  displayName: FormControl<string>;
  defaultFolder: FormControl<string>;
  connectionTimeout: FormControl<number | null>;
  autoReconnect: FormControl<boolean>;
};

type ConnectionFormValue = {
  providerType: EmailProviderType;
  server: string;
  port: number;
  useSsl: boolean;
  username: string;
  password: string;
  displayName: string;
  defaultFolder: string;
  connectionTimeout: number | null;
  autoReconnect: boolean;
};

type TriState = 'any' | 'true' | 'false';

type CriteriaFormControls = {
  name: FormControl<string>;
  description: FormControl<string>;
  folderName: FormControl<string>;
  subjectContains: FormControl<string>;
  fromContains: FormControl<string>;
  toContains: FormControl<string>;
  dateFrom: FormControl<string>;
  dateTo: FormControl<string>;
  isRead: FormControl<TriState>;
  hasAttachments: FormControl<TriState>;
  maxResults: FormControl<number | null>;
  maxDaysBack: FormControl<number | null>;
  skip: FormControl<number | null>;
  sortBy: FormControl<number>;
  sortDescending: FormControl<boolean>;
  includeFlags: FormControl<string>;
  excludeFlags: FormControl<string>;
};

type CriteriaFormValue = {
  name: string;
  description: string;
  folderName: string;
  subjectContains: string;
  fromContains: string;
  toContains: string;
  dateFrom: string;
  dateTo: string;
  isRead: TriState;
  hasAttachments: TriState;
  maxResults: number | null;
  maxDaysBack: number | null;
  skip: number | null;
  sortBy: number;
  sortDescending: boolean;
  includeFlags: string;
  excludeFlags: string;
};



// The API provides EmailSettingsDto for GET; SaveEmailSettingsRequest is used for POST.
// A type-guard helps distinguish the request shape from the DTO so we can keep
// strict typing in conversion helpers.
function isSaveEmailSettingsRequest(value: EmailSettingsDto | SaveEmailSettingsRequest): value is SaveEmailSettingsRequest {
  // SaveEmailSettingsRequest commonly has no readonly metadata fields like id/createdAt etc.
  // We'll detect by presence of password or the lack of DTO-only fields. This is a pragmatic guard.
  const v = value as Record<string, unknown>;
  return typeof v['password'] !== 'undefined' || typeof v['server'] !== 'undefined';
}

type ConnectionStatus = { kind: 'success' | 'error'; message: string };

@Component({
  standalone: true,
  selector: 'app-email-settings',
  imports: [CommonModule, FormsModule, ReactiveFormsModule, TuiButton, TuiTextfield, TuiHint, TuiExpand, TranslocoModule, LocaleAwareDatePipe],
  templateUrl: './email-settings.component.html',
  styleUrl: './email-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmailSettingsComponent implements OnInit {
  private readonly emailService = inject(EmailService);
  private readonly toast = inject(ToastService);
  // Transloco service for runtime translation in TS
  private readonly transloco = inject(TranslocoService);

  // Provide translated labels from Transloco so TS can use the localized strings as well.
  protected get providerOptions(): ReadonlyArray<{ label: string; value: EmailProviderType }> {
    return [
      { label: this.transloco.translate('Settings.Email.Provider.Imap'), value: EmailProviderType.Imap },
      { label: this.transloco.translate('Settings.Email.Provider.Pop3'), value: EmailProviderType.Pop3 },
      { label: this.transloco.translate('Settings.Email.Provider.Exchange'), value: EmailProviderType.Exchange },
    ];
  }

  protected get triStateOptions(): ReadonlyArray<{ label: string; value: TriState }> {
    return [
      { label: this.transloco.translate('Common.All'), value: 'any' },
      { label: this.transloco.translate('Common.Yes'), value: 'true' },
      { label: this.transloco.translate('Common.No'), value: 'false' },
    ];
  }

  protected get sortOptions(): ReadonlyArray<{ label: string; value: number }> {
    return [
      { label: this.transloco.translate('Settings.Email.Sort.Date'), value: EmailSortBy.$0 },
      { label: this.transloco.translate('Settings.Email.Sort.Subject'), value: EmailSortBy.$1 },
      { label: this.transloco.translate('Settings.Email.Sort.Sender'), value: EmailSortBy.$2 },
      { label: this.transloco.translate('Settings.Email.Sort.Size'), value: EmailSortBy.$3 },
    ];
  }

  protected readonly connectionForm = new FormGroup<ConnectionFormControls>({
    providerType: new FormControl<EmailProviderType>(EmailProviderType.Imap, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    server: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    port: new FormControl<number>(993, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(1), Validators.max(65535)],
    }),
    useSsl: new FormControl<boolean>(true, { nonNullable: true }),
    username: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    password: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3)],
    }),
    displayName: new FormControl<string>('', { nonNullable: true }),
    defaultFolder: new FormControl<string>('INBOX', { nonNullable: true }),
    connectionTimeout: new FormControl<number | null>(30000, {
      validators: [Validators.min(1000)],
    }),
    autoReconnect: new FormControl<boolean>(true, { nonNullable: true }),
  });

  // Controls whether the rules (criteria) panel is expanded. Start collapsed by default.
  protected readonly rulesExpanded = signal<boolean>(false);

  protected toggleRules(): void {
    this.rulesExpanded.set(!this.rulesExpanded());
  }

  protected readonly criteriaForm = new FormGroup<CriteriaFormControls>({
    name: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    description: new FormControl<string>('', { nonNullable: true }),
    folderName: new FormControl<string>('INBOX', { nonNullable: true }),
    subjectContains: new FormControl<string>('', { nonNullable: true }),
    fromContains: new FormControl<string>('', { nonNullable: true }),
    toContains: new FormControl<string>('', { nonNullable: true }),
    dateFrom: new FormControl<string>('', { nonNullable: true }),
    dateTo: new FormControl<string>('', { nonNullable: true }),
    isRead: new FormControl<TriState>('any', { nonNullable: true }),
    hasAttachments: new FormControl<TriState>('any', { nonNullable: true }),
    maxResults: new FormControl<number | null>(100, {
      validators: [Validators.min(1)],
    }),
    maxDaysBack: new FormControl<number | null>(30, {
      validators: [Validators.min(1)],
    }),
    skip: new FormControl<number | null>(0, {
      validators: [Validators.min(0)],
    }),
    sortBy: new FormControl<number>(EmailSortBy.$0, { nonNullable: true }),
    sortDescending: new FormControl<boolean>(true, { nonNullable: true }),
    includeFlags: new FormControl<string>('', { nonNullable: true }),
    excludeFlags: new FormControl<string>('', { nonNullable: true }),
  });

  private readonly defaultConnectionValue: ConnectionFormValue = {
    providerType: EmailProviderType.Imap,
    server: '',
    port: 993,
    useSsl: true,
    username: '',
    password: '',
    displayName: '',
    defaultFolder: 'INBOX',
    connectionTimeout: 30000,
    autoReconnect: true,
  };

  private readonly defaultCriteriaValue: CriteriaFormValue = {
    name: '',
    description: '',
    folderName: 'INBOX',
    subjectContains: '',
    fromContains: '',
    toContains: '',
    dateFrom: '',
    dateTo: '',
    isRead: 'any',
    hasAttachments: 'any',
    maxResults: 100,
    maxDaysBack: 30,
    skip: 0,
    sortBy: EmailSortBy.$0,
    sortDescending: true,
    includeFlags: '',
    excludeFlags: '',
  };

  private lastLoadedConnection: ConnectionFormValue = { ...this.defaultConnectionValue };
  private lastLoadedCriteria: CriteriaFormValue = { ...this.defaultCriteriaValue };

  protected readonly loadingSettings = signal(false);
  protected readonly savingSettings = signal(false);
  protected readonly testingConnection = signal(false);
  protected readonly deletingSettings = signal(false);

  protected readonly emailCount = signal<number | null>(null);
  protected readonly loadingCount = signal(false);
  protected readonly lastCountRefresh = signal<Date | null>(null);

  protected readonly connectionStatus = signal<ConnectionStatus | null>(null);
  protected readonly hasExistingSettings = signal(false);
  // When settings are loaded and a password exists on the server we show a mask
  // in the password field instead of the real secret. This flag tracks that state.
  protected readonly passwordMasked = signal(false);

  private static readonly PASSWORD_MASK = '*****';

  protected readonly loadingCriteria = signal(false);
  protected readonly savingCriteria = signal(false);
  protected readonly deletingCriteria = signal(false);
  protected readonly hasCriteria = signal(false);
  private lastLoadedCriteriaId: string | null = null;

  async ngOnInit(): Promise<void> {
    // Subscribe to password field changes to detect when user replaces the masked value.
    this.connectionForm.controls.password.valueChanges.subscribe(v => {
      // If the control contains the mask, keep masked state; otherwise clear it.
      if (v === EmailSettingsComponent.PASSWORD_MASK) {
        this.passwordMasked.set(true);
      } else {
        this.passwordMasked.set(false);
      }
    });

    await Promise.all([this.loadSettings(), this.loadCriteria()]);
  }

  protected async refreshEmailCount(): Promise<void> {
    this.loadingCount.set(true);
    try {
      const response = await firstValueFrom(this.emailService.apiEmailCountGet$Json());
      // Generated client returns Int32ApiResponse
      const value = this.extractCount(response.data ?? null);
      if (value !== null) {
        this.emailCount.set(value);
        this.lastCountRefresh.set(new Date());
      }
      if (response.success === false && response.message) {
        this.toast.error(response.message);
      }
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Email.CountLoadError')));
    } finally {
      this.loadingCount.set(false);
    }
  }

  protected async testConnection(): Promise<void> {
    this.testingConnection.set(true);
    try {
      const response = await firstValueFrom(this.emailService.apiEmailTestConnectionPost$Json());
      // ConnectionTestResultDtoApiResponse
      const success = response.success === true;
      const defaultMsg = success ? this.transloco.translate('Settings.Email.TestConnectionSuccess') : this.transloco.translate('Settings.Email.TestConnectionFailed');
      const displayMessage: string = typeof response.message === 'string' && response.message.length > 0 ? response.message : defaultMsg;
      // For success use toast only; clear inline connectionStatus. For failures set inline error and a toast.
      if (success) {
        this.connectionStatus.set(null);
        this.toast.success(displayMessage);
      } else {
        this.connectionStatus.set({ kind: 'error', message: displayMessage });
        this.toast.error(displayMessage);
      }
    } catch (error) {
      const messageText: string = this.toErrorMessage(error, this.transloco.translate('Settings.Email.TestConnectionFailed'));
      this.connectionStatus.set({ kind: 'error', message: messageText });
      this.toast.error(messageText);
    } finally {
      this.testingConnection.set(false);
    }
  }

  protected async saveSettings(): Promise<void> {
    this.connectionForm.markAllAsTouched();
    if (this.connectionForm.invalid) {
      this.toast.error(this.transloco.translate('Settings.Email.ConnectionValidationError'));
      return;
    }

    this.savingSettings.set(true);
    const value = this.connectionForm.getRawValue();
    // Build the request as a Partial so we can omit the password when it's only the mask.
    const reqPartial: Partial<SaveEmailSettingsRequest> = {
      providerType: value.providerType,
      server: value.server.trim(),
      port: value.port,
      useSsl: value.useSsl,
      username: value.username.trim(),
      displayName: this.trimToNull(value.displayName) ?? value.username.trim(),
      defaultFolder: this.trimToNull(value.defaultFolder) ?? undefined,
      connectionTimeout: value.connectionTimeout ?? undefined,
      autoReconnect: value.autoReconnect,
    };

    // Only include password when user actually entered one (not the mask)
    if (!(this.passwordMasked() && value.password === EmailSettingsComponent.PASSWORD_MASK) && value.password && value.password.length > 0) {
      reqPartial.password = value.password;
    }
    const request = reqPartial as SaveEmailSettingsRequest;

    try {
      const response = await firstValueFrom(this.emailService.apiEmailSettingsPost$Json({ body: request }));
      // response is EmailSettingsDtoApiResponse
      if (response.success === false && response.message) {
        this.toast.error(response.message);
        return;
      }
  const defaultMsg = this.transloco.translate('Settings.Email.Saved');
  const displayMessage: string = typeof response.message === 'string' && response.message.length > 0 ? response.message : defaultMsg;
  this.toast.success(displayMessage);
  // reflect saved values in the form. If the password was omitted in the request
  // but settings exist on the server, we keep showing the mask.
  const nextValue = this.toConnectionFormValue(request);
  this.hasExistingSettings.set(true);
  this.applyConnectionValue(nextValue, true);
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Email.SaveError')));
    } finally {
      this.savingSettings.set(false);
    }
  }

  protected resetConnectionForm(): void {
    // Patch non-sensitive fields so booleans are restored. If settings exist on
    // the server, restore the visual mask as well; otherwise clear the password.
    const source = this.hasExistingSettings() ? this.lastLoadedConnection : this.defaultConnectionValue;
    const patch = {
      providerType: source.providerType,
      server: source.server,
      port: source.port,
      useSsl: source.useSsl,
      username: source.username,
      displayName: source.displayName,
      defaultFolder: source.defaultFolder,
      connectionTimeout: source.connectionTimeout,
      autoReconnect: source.autoReconnect,
    } as Partial<ConnectionFormValue>;

    this.connectionForm.patchValue(patch);
    if (this.hasExistingSettings()) {
      // restore the mask to indicate a server-side password exists
      this.connectionForm.controls.password.setValue(EmailSettingsComponent.PASSWORD_MASK, { emitEvent: false });
      this.passwordMasked.set(true);
    } else {
      this.connectionForm.controls.password.setValue('', { emitEvent: false });
      this.passwordMasked.set(false);
    }
    this.connectionForm.markAsPristine();
    Object.values(this.connectionForm.controls).forEach(control => control.markAsUntouched());
  }

  // Returns true when the current connection form differs from the last loaded values
  protected hasConnectionChanges(): boolean {
    const current = this.connectionForm.getRawValue();
    const last = this.lastLoadedConnection;
    // Compare relevant fields; ignore password because we store it masked/empty.
    const baseChanged = (
      current.providerType !== last.providerType ||
      (current.server ?? '').trim() !== (last.server ?? '').trim() ||
      current.port !== last.port ||
      current.useSsl !== last.useSsl ||
      (current.username ?? '').trim() !== (last.username ?? '').trim() ||
      (current.displayName ?? '').trim() !== (last.displayName ?? '').trim() ||
      (current.defaultFolder ?? '').trim() !== (last.defaultFolder ?? '').trim() ||
      (current.connectionTimeout ?? null) !== (last.connectionTimeout ?? null) ||
      current.autoReconnect !== last.autoReconnect
    );

    // Also consider password changes: if the control contains a non-empty value
    // that is not the mask, treat it as a change.
    const pw = this.connectionForm.controls.password.value ?? '';
    const passwordChanged = typeof pw === 'string' && pw.length > 0 && pw !== EmailSettingsComponent.PASSWORD_MASK;

    return baseChanged || passwordChanged;
  }

  // Returns true when criteria form differs from last loaded criteria
  protected hasCriteriaChanges(): boolean {
    const cur = this.criteriaForm.getRawValue();
    const last = this.lastLoadedCriteria;
    return (
      (cur.name ?? '').trim() !== (last.name ?? '').trim() ||
      (cur.description ?? '').trim() !== (last.description ?? '').trim() ||
      (cur.folderName ?? '').trim() !== (last.folderName ?? '').trim() ||
      (cur.subjectContains ?? '').trim() !== (last.subjectContains ?? '').trim() ||
      (cur.fromContains ?? '').trim() !== (last.fromContains ?? '').trim() ||
      (cur.toContains ?? '').trim() !== (last.toContains ?? '').trim() ||
      (cur.dateFrom ?? '') !== (last.dateFrom ?? '') ||
      (cur.dateTo ?? '') !== (last.dateTo ?? '') ||
      cur.isRead !== last.isRead ||
      cur.hasAttachments !== last.hasAttachments ||
      (this.coerceNumber(cur.maxResults) ?? null) !== (this.coerceNumber(last.maxResults) ?? null) ||
      (this.coerceNumber(cur.maxDaysBack) ?? null) !== (this.coerceNumber(last.maxDaysBack) ?? null) ||
      (this.coerceNumber(cur.skip) ?? null) !== (this.coerceNumber(last.skip) ?? null) ||
      cur.sortBy !== last.sortBy ||
      cur.sortDescending !== last.sortDescending ||
      (cur.includeFlags ?? '').trim() !== (last.includeFlags ?? '').trim() ||
      (cur.excludeFlags ?? '').trim() !== (last.excludeFlags ?? '').trim()
    );
  }

  protected async deleteSettings(): Promise<void> {
    if (!this.hasExistingSettings()) {
      this.applyConnectionValue(this.defaultConnectionValue);
      return;
    }

    this.deletingSettings.set(true);
    try {
      const response = await firstValueFrom(this.emailService.apiEmailSettingsDelete$Json());
      const message = response.message ?? 'E-Mail-Einstellungen wurden entfernt.';
      this.toast.success(message);
      this.hasExistingSettings.set(false);
      this.applyConnectionValue(this.defaultConnectionValue);
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, 'E-Mail-Einstellungen konnten nicht entfernt werden.'));
    } finally {
      this.deletingSettings.set(false);
    }
  }

  protected shouldShowConnectionError(control: keyof ConnectionFormControls): boolean {
    const ctrl = this.connectionForm.controls[control];
    return ctrl.invalid && (ctrl.dirty || ctrl.touched);
  }

  protected async saveCriteria(): Promise<void> {
    this.criteriaForm.markAllAsTouched();
    if (this.criteriaForm.invalid) {
  this.toast.error(this.transloco.translate('Settings.Email.CriteriaValidationError'));
      return;
    }

    this.savingCriteria.set(true);
    const value = this.criteriaForm.getRawValue();
    const request: SaveEmailCriteriaRequest = {
      name: value.name.trim(),
      description: this.trimToNull(value.description),
      folderName: this.trimToNull(value.folderName) ?? undefined,
      subjectContains: this.trimToNull(value.subjectContains),
      fromContains: this.trimToNull(value.fromContains),
      toContains: this.trimToNull(value.toContains),
      dateFrom: this.trimToNull(value.dateFrom),
      dateTo: this.trimToNull(value.dateTo),
      isRead: this.fromTriState(value.isRead),
      hasAttachments: this.fromTriState(value.hasAttachments),
      maxResults: this.coerceNumber(value.maxResults) ?? undefined,
  maxDaysBack: this.coerceNumber(value.maxDaysBack) ?? undefined,
      skip: this.coerceNumber(value.skip) ?? undefined,
      sortBy: value.sortBy as EmailSortBy,
      sortDescending: value.sortDescending,
      includeFlags: this.parseFlags(value.includeFlags) ?? undefined,
      excludeFlags: this.parseFlags(value.excludeFlags) ?? undefined,
    };

    try {
      const response = await firstValueFrom(this.emailService.apiEmailCriteriaPost$Json({ body: request }));
      if (response.success === false && response.message) {
        this.toast.error(response.message);
        return;
      }
      const dto = response.data ?? this.createCriteriaFromRequest(request);
      this.applyCriteriaValue(this.toCriteriaFormValue(dto));
      const id = (dto as any).id ?? null;
      this.lastLoadedCriteriaId = typeof id === 'string' && id.length > 0 ? id : null;
      const has = this.isNonZeroGuid(this.lastLoadedCriteriaId);
      this.hasCriteria.set(has);
      // Keep rules panel open after save if criteria exist
      this.rulesExpanded.set(has);
  const defaultCriteriaSaved = this.transloco.translate('Settings.Email.CriteriaSaved');
  const criteriaDisplayMessage: string = typeof response.message === 'string' && response.message.length > 0 ? response.message : defaultCriteriaSaved;
  this.toast.success(criteriaDisplayMessage);
    } catch (error) {
  this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Email.CriteriaSaveError')));
    } finally {
      this.savingCriteria.set(false);
    }
  }

  protected shouldShowCriteriaError(control: keyof CriteriaFormControls): boolean {
    const ctrl = this.criteriaForm.controls[control];
    return ctrl.invalid && (ctrl.dirty || ctrl.touched);
  }

  protected resetCriteriaForm(): void {
    if (this.hasCriteria()) {
      this.applyCriteriaValue(this.lastLoadedCriteria);
    } else {
      this.applyCriteriaValue(this.defaultCriteriaValue);
    }
  }

  protected async deleteCriteria(): Promise<void> {
    if (!this.hasCriteria()) {
      this.applyCriteriaValue(this.defaultCriteriaValue);
      return;
    }

    this.deletingCriteria.set(true);
    try {
      await firstValueFrom(this.emailService.apiEmailCriteriaDelete$Json());
  this.toast.success(this.transloco.translate('Settings.Email.CriteriaDeleted'));
      this.hasCriteria.set(false);
      this.applyCriteriaValue(this.defaultCriteriaValue);
      // Close the rules panel after deletion
      this.rulesExpanded.set(false);
    } catch (error) {
  this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Email.CriteriaDeleteError')));
    } finally {
      this.deletingCriteria.set(false);
    }
  }

  private async loadSettings(): Promise<void> {
    this.loadingSettings.set(true);
    try {
      const response = await firstValueFrom(this.emailService.apiEmailSettingsGet$Json());
      if (response.success === false && response.message) {
        this.toast.error(response.message);
      }
      if (response.data) {
        const view = this.toConnectionFormValue(response.data);
        // server DTO contained credentials (we don't populate the real password),
        // but indicate that a password exists so the UI shows a mask.
        this.applyConnectionValue(view, true);
        this.hasExistingSettings.set(true);
      } else {
        this.applyConnectionValue(this.defaultConnectionValue);
        this.hasExistingSettings.set(false);
      }
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.applyConnectionValue(this.defaultConnectionValue);
        this.hasExistingSettings.set(false);
      } else {
    this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Email.LoadError')));
      }
    } finally {
      this.loadingSettings.set(false);
    }
  }

  private async loadCriteria(): Promise<void> {
    this.loadingCriteria.set(true);
    try {
      const response = await firstValueFrom(this.emailService.apiEmailCriteriaGet$Json());
      if (response.success === false && response.message) {
  this.toast.error(response.message);
      }
      if (response.data) {
        this.applyCriteriaValue(this.toCriteriaFormValue(response.data));
        const id = (response.data as any).id ?? null;
        this.lastLoadedCriteriaId = typeof id === 'string' && id.length > 0 ? id : null;
        const has = this.isNonZeroGuid(this.lastLoadedCriteriaId);
        this.hasCriteria.set(has);
        // Auto-open rules panel when criteria exist
        this.rulesExpanded.set(has);
      } else {
        this.applyCriteriaValue(this.defaultCriteriaValue);
        this.lastLoadedCriteriaId = null;
        this.hasCriteria.set(false);
        // Close rules panel when no criteria
        this.rulesExpanded.set(false);
      }
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.applyCriteriaValue(this.defaultCriteriaValue);
        this.hasCriteria.set(false);
      } else {
    this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Email.CriteriaLoadError')));
      }
    } finally {
      this.loadingCriteria.set(false);
    }
  }

  private applyConnectionValue(value: ConnectionFormValue, keepPasswordMask = false): void {
    // If the caller wants to keep the mask (e.g., after save where we didn't send a new password),
    // show the mask in the password input. Internally we store lastLoadedConnection with an
    // empty password to avoid leaking secrets.
  // If keepPasswordMask is requested (typically when settings exist on server),
  // show the mask to indicate a password is set. We don't rely on the DTO
  // containing the actual password field.
  const displayPassword = keepPasswordMask ? EmailSettingsComponent.PASSWORD_MASK : '';
    const sanitized: ConnectionFormValue = { ...value, password: '' };
    this.connectionForm.reset({ ...sanitized, password: displayPassword });
    this.connectionForm.controls.password.setValue(displayPassword, { emitEvent: false });
    this.connectionForm.markAsPristine();
    Object.values(this.connectionForm.controls).forEach(control => control.markAsUntouched());
    this.lastLoadedConnection = { ...sanitized };
    this.passwordMasked.set(displayPassword === EmailSettingsComponent.PASSWORD_MASK);
  }

  private applyCriteriaValue(value: CriteriaFormValue): void {
    const sanitized: CriteriaFormValue = { ...value };
    this.criteriaForm.reset(sanitized);
    this.criteriaForm.markAsPristine();
    Object.values(this.criteriaForm.controls).forEach(control => control.markAsUntouched());
    this.lastLoadedCriteria = { ...sanitized };
  }

  // parseCriteriaResponse removed — generated client returns EmailCriteriaDtoApiResponse

  private toConnectionFormValue(payload: EmailSettingsDto | SaveEmailSettingsRequest): ConnectionFormValue {
    if (isSaveEmailSettingsRequest(payload)) {
      const req = payload as SaveEmailSettingsRequest;
      return {
        providerType: req.providerType ?? EmailProviderType.Imap,
        server: (req.server ?? '').trim(),
        port: typeof req.port === 'number' ? req.port : 993,
        useSsl: req.useSsl ?? true,
        username: (req.username ?? '').trim(),
        password: req.password ?? '',
        displayName: (req.displayName ?? '').trim(),
        defaultFolder: (req.defaultFolder ?? 'INBOX').trim(),
        connectionTimeout: typeof req.connectionTimeout === 'number' ? req.connectionTimeout : 30000,
        autoReconnect: req.autoReconnect ?? true,
      };
    }

    // Otherwise treat as DTO
    const dto = payload as EmailSettingsDto;
    return {
      providerType: dto.providerType ?? EmailProviderType.Imap,
      server: (dto.server ?? '').trim(),
      port: typeof dto.port === 'number' ? dto.port : 993,
      useSsl: dto.useSsl ?? true,
      username: (dto.username ?? '').trim(),
      password: '',
      displayName: (dto.displayName ?? '').trim(),
      defaultFolder: (dto.defaultFolder ?? 'INBOX').trim(),
      connectionTimeout: typeof dto.connectionTimeout === 'number' ? dto.connectionTimeout : 30000,
      autoReconnect: dto.autoReconnect ?? true,
    };
  }

  private toCriteriaFormValue(dto: EmailCriteriaDto | CriteriaFormValue): CriteriaFormValue {
    return {
      name: dto.name?.trim() ?? '',
      description: dto.description?.trim() ?? '',
      folderName: dto.folderName?.trim() || 'INBOX',
      subjectContains: dto.subjectContains?.trim() ?? '',
      fromContains: dto.fromContains?.trim() ?? '',
      toContains: dto.toContains?.trim() ?? '',
      dateFrom: this.toDateInput(dto.dateFrom),
      dateTo: this.toDateInput(dto.dateTo),
      isRead: this.toTriState(dto.isRead),
      hasAttachments: this.toTriState(dto.hasAttachments),
      maxResults: dto.maxResults ?? 100,
      maxDaysBack: dto.maxDaysBack ?? 30,
      skip: dto.skip ?? 0,
      sortBy: dto.sortBy ?? EmailSortBy.$0,
      sortDescending: dto.sortDescending ?? true,
      includeFlags: this.joinFlags(dto.includeFlags),
      excludeFlags: this.joinFlags(dto.excludeFlags),
    };
  }

  private createCriteriaFromRequest(request: SaveEmailCriteriaRequest): EmailCriteriaDto {
    return {
      name: request.name ?? '',
      description: request.description ?? '',
      folderName: request.folderName ?? 'INBOX',
      subjectContains: request.subjectContains ?? '',
      fromContains: request.fromContains ?? '',
      toContains: request.toContains ?? '',
      dateFrom: request.dateFrom ?? undefined,
      dateTo: request.dateTo ?? undefined,
      isRead: request.isRead ?? null,
      hasAttachments: request.hasAttachments ?? null,
      maxResults: request.maxResults ?? undefined,
      maxDaysBack: request.maxDaysBack ?? undefined,
      skip: request.skip ?? undefined,
      sortBy: request.sortBy ?? EmailSortBy.$0,
      sortDescending: request.sortDescending ?? true,
      includeFlags: request.includeFlags ?? undefined,
      excludeFlags: request.excludeFlags ?? undefined,
      id: '',
      createdAt: '',
      updatedAt: '',
    };
  }

  private extractCount(value: number | null): number | null;
  private extractCount(value: Int32ApiResponse | null): number | null;
  private extractCount(value: number | Int32ApiResponse | null): number | null {
    if (typeof value === 'number') {
      return value;
    }
    if (value && typeof value === 'object') {
      const record = value as Int32ApiResponse;
      if (typeof record.data === 'number') {
        return record.data;
      }
      const candidate = (record as Record<string, unknown>)['count'];
      if (typeof candidate === 'number') {
        return candidate;
      }
    }
    return null;
  }

  private extractSuccess(value: unknown): boolean {
    if (value && typeof value === 'object') {
      const record = value as Record<string, unknown>;
      const success = record['success'];
      if (typeof success === 'boolean') {
        return success;
      }
    }
    return false;
  }

  private trimToNull(input: string | null | undefined): string | null {
    if (input == null) {
      return null;
    }
    const trimmed = input.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  private parseFlags(value: string | null | undefined): string[] | null {
    if (!value) {
      return null;
    }
    const items = value
      .split(',')
      .map(item => item.trim())
      .filter(item => item.length > 0);
    return items.length > 0 ? items : null;
  }

  private toTriState(value: boolean | TriState | null | undefined): TriState {
    if (value === true) {
      return 'true';
    }
    if (value === false) {
      return 'false';
    }
    return 'any';
  }

  private fromTriState(value: TriState): boolean | null {
    if (value === 'true') {
      return true;
    }
    if (value === 'false') {
      return false;
    }
    return null;
  }

  private toDateInput(value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return date.toISOString().slice(0, 10);
  }

  private joinFlags(flags: string | ReadonlyArray<string> | null | undefined): string {
    if (!flags) {
      return '';
    }
    if (typeof flags === 'string') {
      return flags;
    }
    return flags.join(', ');
  }

  private coerceNumber(value: number | null): number | null {
    if (value == null || Number.isNaN(value)) {
      return null;
    }
    return value;
  }

  private isNonZeroGuid(id: string | null): boolean {
    if (!id) return false;
    const zeroGuid = '00000000-0000-0000-0000-000000000000';
    return id !== zeroGuid;
  }

  private toErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const data = error.error as Record<string, unknown> | string | undefined;
      if (typeof data === 'string') {
        return data;
      }
      if (data && typeof data === 'object') {
        const message = data['message'];
        if (typeof message === 'string' && message.trim().length > 0) {
          return message;
        }
      }
      return error.message || fallback;
    }
    if (error && typeof error === 'object') {
      const maybeMessage = (error as Record<string, unknown>)['message'];
      if (typeof maybeMessage === 'string' && maybeMessage.trim().length > 0) {
        return maybeMessage;
      }
    }
    return fallback;
  }
}
