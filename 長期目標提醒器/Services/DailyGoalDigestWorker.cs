using Microsoft.Extensions.Options;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public sealed class DailyGoalDigestWorker(
    EmailService emailService,
    EmailScheduleStore scheduleStore,
    IOptions<EmailSettings> options,
    ILogger<DailyGoalDigestWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim signal = new(0, 1);
    private readonly TimeZoneInfo timeZone = AppTime.Resolve(options.Value.TimeZone);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        scheduleStore.Changed += OnScheduleChanged;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var schedule = scheduleStore.Current;

                if (!schedule.Enabled)
                {
                    logger.LogInformation("目標摘要信目前為停用狀態，等待設定變更。");
                    if (!await WaitForChangeAsync(Timeout.InfiniteTimeSpan, stoppingToken))
                    {
                        break;
                    }

                    continue;
                }

                if (!emailService.IsConfigured(schedule))
                {
                    logger.LogWarning("寄件帳號或收件者未設定，摘要信不會寄出。等待設定變更。");
                    if (!await WaitForChangeAsync(Timeout.InfiniteTimeSpan, stoppingToken))
                    {
                        break;
                    }

                    continue;
                }

                var delay = DigestSchedule.DelayUntilNext(schedule, timeZone);
                var next = AppTime.NowLocal(timeZone).Add(delay);

                logger.LogInformation(
                    "下一封目標摘要信：{Next:yyyy/MM/dd HH:mm}（{Description}），約 {Minutes:0} 分鐘後",
                    next,
                    DigestSchedule.Describe(schedule),
                    delay.TotalMinutes);

                if (await WaitForChangeAsync(delay, stoppingToken))
                {
                    logger.LogInformation("寄信排程已變更，重新計算下一封寄送時間。");
                    continue;
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await emailService.SendGoalDigestAsync(GoalSeedData.Goals, schedule, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "目標摘要信寄送失敗，將於下一個排定時間重試。");
                }
            }
        }
        finally
        {
            scheduleStore.Changed -= OnScheduleChanged;
        }
    }

    private async Task<bool> WaitForChangeAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await signal.WaitAsync(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void OnScheduleChanged()
    {
        try
        {
            signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // 已經有待處理的變更通知，不需要重複。
        }
    }
}
