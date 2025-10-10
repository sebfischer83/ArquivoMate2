import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TuiButton, TuiTextfield } from '@taiga-ui/core';
import { TuiSwitch } from '@taiga-ui/kit';
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
  imports: [CommonModule, ReactiveFormsModule, TuiButton, TuiTextfield, TuiSwitch],
  templateUrl: './email-settings.component.html',
  styleUrl: './email-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmailSettingsComponent implements OnInit {
  private readonly emailService = inject(EmailService);
  private readonly toast = inject(ToastService);

  protected readonly providerOptions: ReadonlyArray<{ label: string; value: EmailProviderType }> = [
    { label: 'IMAP', value: EmailProviderType.Imap },
    { label: 'POP3', value: EmailProviderType.Pop3 },
    { label: 'Exchange', value: EmailProviderType.Exchange },
  ];

  protected readonly triStateOptions: ReadonlyArray<{ label: string; value: TriState }> = [
    { label: 'Alle', value: 'any' },
    { label: 'Ja', value: 'true' },
    { label: 'Nein', value: 'false' },
  ];

  protected readonly sortOptions: ReadonlyArray<{ label: string; value: number }> = [
    { label: 'Datum', value: EmailSortBy.$0 },
    { label: 'Betreff', value: EmailSortBy.$1 },
    { label: 'Absender', value: EmailSortBy.$2 },
    { label: 'Größe', value: EmailSortBy.$3 },
  ];

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

  protected readonly loadingCriteria = signal(false);
  protected readonly savingCriteria = signal(false);
  protected readonly deletingCriteria = signal(false);
  protected readonly hasCriteria = signal(false);

  async ngOnInit(): Promise<void> {
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
      this.toast.error(this.toErrorMessage(error, 'E-Mail-Zähler konnte nicht abgerufen werden.'));
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
      const message = response.message ?? (success ? 'Verbindung erfolgreich getestet.' : 'Verbindung fehlgeschlagen.');
      this.connectionStatus.set({ kind: success ? 'success' : 'error', message });
      if (success) {
        this.toast.success(message);
      } else {
        this.toast.error(message);
      }
    } catch (error) {
      const message = this.toErrorMessage(error, 'Verbindungstest fehlgeschlagen.');
      this.connectionStatus.set({ kind: 'error', message });
      this.toast.error(message);
    } finally {
      this.testingConnection.set(false);
    }
  }

  protected async saveSettings(): Promise<void> {
    this.connectionForm.markAllAsTouched();
    if (this.connectionForm.invalid) {
      this.toast.error('Bitte prüfe die Eingaben für die Mail-Verbindung.');
      return;
    }

    this.savingSettings.set(true);
    const value = this.connectionForm.getRawValue();
    const request: SaveEmailSettingsRequest = {
      providerType: value.providerType,
      server: value.server.trim(),
      port: value.port,
      useSsl: value.useSsl,
      username: value.username.trim(),
      password: value.password,
      displayName: this.trimToNull(value.displayName) ?? value.username.trim(),
      defaultFolder: this.trimToNull(value.defaultFolder) ?? undefined,
      connectionTimeout: value.connectionTimeout ?? undefined,
      autoReconnect: value.autoReconnect,
    };

    try {
      const response = await firstValueFrom(this.emailService.apiEmailSettingsPost$Json({ body: request }));
      // response is EmailSettingsDtoApiResponse
      if (response.success === false && response.message) {
        this.toast.error(response.message);
        return;
      }
      const message = response.message ?? 'E-Mail-Einstellungen wurden gespeichert.';
      this.toast.success(message);
      const nextValue = this.toConnectionFormValue(request);
      this.hasExistingSettings.set(true);
      this.applyConnectionValue(nextValue);
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, 'E-Mail-Einstellungen konnten nicht gespeichert werden.'));
    } finally {
      this.savingSettings.set(false);
    }
  }

  protected resetConnectionForm(): void {
    if (this.hasExistingSettings()) {
      this.applyConnectionValue(this.lastLoadedConnection);
    } else {
      this.applyConnectionValue(this.defaultConnectionValue);
    }
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
      this.toast.error('Bitte prüfe die Angaben für die Abruf-Regeln.');
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
      this.hasCriteria.set(true);
      const message = response.message ?? 'Abruf-Regeln gespeichert.';
      this.toast.success(message);
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, 'Abruf-Regeln konnten nicht gespeichert werden.'));
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
      this.toast.success('Abruf-Regeln wurden entfernt.');
      this.hasCriteria.set(false);
      this.applyCriteriaValue(this.defaultCriteriaValue);
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, 'Abruf-Regeln konnten nicht entfernt werden.'));
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
        this.applyConnectionValue(view);
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
        this.toast.error(this.toErrorMessage(error, 'E-Mail-Einstellungen konnten nicht geladen werden.'));
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
        this.hasCriteria.set(true);
      } else {
        this.applyCriteriaValue(this.defaultCriteriaValue);
        this.hasCriteria.set(false);
      }
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.applyCriteriaValue(this.defaultCriteriaValue);
        this.hasCriteria.set(false);
      } else {
        this.toast.error(this.toErrorMessage(error, 'Abruf-Regeln konnten nicht geladen werden.'));
      }
    } finally {
      this.loadingCriteria.set(false);
    }
  }

  private applyConnectionValue(value: ConnectionFormValue): void {
    const sanitized: ConnectionFormValue = { ...value, password: '' };
    this.connectionForm.reset(sanitized);
    this.connectionForm.controls.password.setValue('', { emitEvent: false });
    this.connectionForm.markAsPristine();
    Object.values(this.connectionForm.controls).forEach(control => control.markAsUntouched());
    this.lastLoadedConnection = { ...sanitized };
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
